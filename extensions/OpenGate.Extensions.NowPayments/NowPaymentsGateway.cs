using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.NowPayments;

/// <summary>
/// NOWPayments cryptocurrency gateway. Uses <see cref="IHttpClientFactory"/>
/// when available so DNS rotation and connection reuse are handled by the host.
/// </summary>
public class NowPaymentsGateway : IPaymentGateway
{
    private static readonly Uri ApiBase = new("https://api.nowpayments.io/v1/");
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly Lazy<HttpClient> _fallbackClient = new(() => new HttpClient { BaseAddress = ApiBase, Timeout = TimeSpan.FromSeconds(30) });
    private string _apiKey = string.Empty;
    private string _ipnSecret = string.Empty;

    /// <summary>
    /// Creates the gateway, resolving an <see cref="IHttpClientFactory"/> if
    /// the host registered one.
    /// </summary>
    public NowPaymentsGateway(IServiceProvider? serviceProvider = null)
    {
        _httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory?.CreateClient("OpenGate.Default") ?? _fallbackClient.Value;
        client.BaseAddress = ApiBase;
        return client;
    }

    public string Name => "nowpayments";
    public string DisplayName => "NOWPayments";
    public string Version => "1.0.0";
    public string? Description => "NOWPayments cryptocurrency payment gateway integration";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _apiKey = settings.GetValueOrDefault("ApiKey", string.Empty);
        _ipnSecret = settings.GetValueOrDefault("IpnSecret", string.Empty);
        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["ApiKey"] = "",
            ["IpnSecret"] = ""
        };
    }

    public async Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var body = new Dictionary<string, object>
            {
                ["price_amount"] = request.Amount,
                ["price_currency"] = request.Currency.ToLowerInvariant(),
                ["order_id"] = request.InvoiceId,
                ["order_description"] = request.Description,
                ["ipn_callback_url"] = request.Metadata.GetValueOrDefault("WebhookUrl", ""),
                ["success_url"] = request.ReturnUrl,
                ["cancel_url"] = request.CancelUrl
            };

            if (request.Metadata.TryGetValue("PayCurrency", out var payCurrency) && !string.IsNullOrEmpty(payCurrency))
                body["pay_currency"] = payCurrency;

            var response = await SendRequestAsync("invoice", body);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = $"NOWPayments API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idProp) ? idProp.ToString() : null;
            var invoiceUrl = root.TryGetProperty("invoice_url", out var urlProp) ? urlProp.GetString() : null;

            return new PaymentResult
            {
                Success = true,
                TransactionId = id,
                PaymentUrl = invoiceUrl,
                AmountPaid = 0
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<PaymentResult> VerifyPaymentAsync(string transactionId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"payment/{transactionId}");
            request.Headers.Add("x-api-key", _apiKey);

            var response = await CreateClient().SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"NOWPayments API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var status = root.TryGetProperty("payment_status", out var sProp) ? sProp.GetString() : "";
            var paid = root.TryGetProperty("actually_paid", out var pProp) ? pProp.GetDouble() : 0;

            return new PaymentResult
            {
                Success = status is "finished" or "confirmed",
                TransactionId = transactionId,
                AmountPaid = (decimal)paid,
                ErrorMessage = status is "finished" or "confirmed" ? null : $"Status: {status}"
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public Task<PaymentResult> RefundAsync(string transactionId, decimal amount)
    {
        return Task.FromResult(new PaymentResult
        {
            Success = false,
            TransactionId = transactionId,
            ErrorMessage = "Refunds are not supported for cryptocurrency payments via NOWPayments."
        });
    }

    public async Task<string> GetPaymentUrl(PaymentRequest request)
    {
        var result = await CreatePaymentAsync(request);
        return result.PaymentUrl ?? string.Empty;
    }

    public Task<WebhookResult> HandleWebhookAsync(string payload, Dictionary<string, string> headers)
    {
        try
        {
            if (string.IsNullOrEmpty(_ipnSecret))
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });

            var receivedSig = headers.GetValueOrDefault("x-nowpayments-sig", "");
            if (string.IsNullOrEmpty(receivedSig))
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });

            {
                using var doc = JsonDocument.Parse(payload);
                var sorted = new SortedDictionary<string, JsonElement>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    sorted[prop.Name] = prop.Value.Clone();

                var sortedJson = JsonSerializer.Serialize(sorted);
                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_ipnSecret));
                var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(sortedJson));

                byte[] received;
                try
                {
                    received = Convert.FromHexString(receivedSig);
                }
                catch (FormatException)
                {
                    return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
                }

                if (!CryptographicOperations.FixedTimeEquals(expected, received))
                {
                    return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
                }
            }

            using var jsonDoc = JsonDocument.Parse(payload);
            var root = jsonDoc.RootElement;

            var status = root.TryGetProperty("payment_status", out var statusProp) ? statusProp.GetString() : "";
            var orderId = root.TryGetProperty("order_id", out var orderProp) ? orderProp.GetString() : null;
            var paymentId = root.TryGetProperty("payment_id", out var pidProp) ? pidProp.ToString() : null;
            var actuallyPaid = root.TryGetProperty("actually_paid", out var paidProp) ? paidProp.GetDouble() : 0;

            var eventType = status switch
            {
                "finished" or "confirmed" => WebhookEventType.PaymentCompleted,
                "failed" or "expired" => WebhookEventType.PaymentFailed,
                "refunded" => WebhookEventType.PaymentRefunded,
                _ => WebhookEventType.Other
            };

            return Task.FromResult(new WebhookResult
            {
                Success = true,
                TransactionId = paymentId,
                InvoiceId = orderId,
                EventType = eventType,
                Amount = (decimal)actuallyPaid
            });
        }
        catch
        {
            return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
        }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, Dictionary<string, object> body)
    {
        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);

        return await CreateClient().SendAsync(request);
    }
}
