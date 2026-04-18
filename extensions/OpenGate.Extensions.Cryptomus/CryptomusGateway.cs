using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.Cryptomus;

/// <summary>
/// Cryptomus cryptocurrency gateway. Uses <see cref="IHttpClientFactory"/>
/// when available so the host controls socket lifetimes and DNS rotation.
/// </summary>
public class CryptomusGateway : IPaymentGateway
{
    private static readonly Uri ApiBase = new("https://api.cryptomus.com/v1/");
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly Lazy<HttpClient> _fallbackClient = new(() => new HttpClient { BaseAddress = ApiBase, Timeout = TimeSpan.FromSeconds(30) });
    private string _merchantId = string.Empty;
    private string _apiKey = string.Empty;

    /// <summary>
    /// Creates the gateway, resolving an <see cref="IHttpClientFactory"/> if
    /// the host registered one.
    /// </summary>
    public CryptomusGateway(IServiceProvider? serviceProvider = null)
    {
        _httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory?.CreateClient("OpenGate.Default") ?? _fallbackClient.Value;
        client.BaseAddress = ApiBase;
        return client;
    }

    public string Name => "cryptomus";
    public string DisplayName => "Cryptomus";
    public string Version => "1.0.0";
    public string? Description => "Cryptomus cryptocurrency payment gateway integration";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _merchantId = settings.GetValueOrDefault("MerchantId", string.Empty);
        _apiKey = settings.GetValueOrDefault("ApiKey", string.Empty);
        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["MerchantId"] = "",
            ["ApiKey"] = ""
        };
    }

    public async Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var body = new Dictionary<string, object>
            {
                ["amount"] = request.Amount.ToString("F2"),
                ["currency"] = request.Currency,
                ["order_id"] = request.InvoiceId,
                ["url_callback"] = request.Metadata.GetValueOrDefault("WebhookUrl", ""),
                ["url_success"] = request.ReturnUrl,
                ["url_return"] = request.CancelUrl
            };

            if (request.Metadata.TryGetValue("Network", out var network) && !string.IsNullOrEmpty(network))
                body["network"] = network;

            if (request.Metadata.TryGetValue("Lifetime", out var lifetime) && !string.IsNullOrEmpty(lifetime))
                body["lifetime"] = int.Parse(lifetime);

            var response = await SendRequestAsync("payment", body);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = $"Cryptomus API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var result = doc.RootElement.TryGetProperty("result", out var r) ? r : doc.RootElement;

            var uuid = result.TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
            var paymentUrl = result.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

            return new PaymentResult
            {
                Success = true,
                TransactionId = uuid,
                PaymentUrl = paymentUrl,
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
            var body = new Dictionary<string, object> { ["uuid"] = transactionId };
            var response = await SendRequestAsync("payment/info", body);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"Cryptomus API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var result = doc.RootElement.TryGetProperty("result", out var r) ? r : doc.RootElement;
            var status = result.TryGetProperty("status", out var sProp) ? sProp.GetString() : "";
            var paid = result.TryGetProperty("payment_amount", out var pProp) ? pProp.GetString() : "0";

            return new PaymentResult
            {
                Success = status is "paid" or "paid_over",
                TransactionId = transactionId,
                AmountPaid = decimal.TryParse(paid, out var amt) ? amt : 0,
                ErrorMessage = status is "paid" or "paid_over" ? null : $"Status: {status}"
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
            ErrorMessage = "Refunds are not supported for cryptocurrency payments via Cryptomus."
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
            if (string.IsNullOrEmpty(_apiKey))
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var receivedSign = root.TryGetProperty("sign", out var signProp) ? signProp.GetString() : null;

            if (string.IsNullOrEmpty(receivedSign))
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });

            {
                var mutable = new Dictionary<string, JsonElement>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name != "sign")
                        mutable[prop.Name] = prop.Value.Clone();
                }

                var bodyWithoutSign = JsonSerializer.Serialize(mutable);
                var expectedSign = ComputeSign(bodyWithoutSign);

                if (!FixedTimeHexEquals(expectedSign, receivedSign))
                {
                    return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
                }
            }

            var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "";
            var orderId = root.TryGetProperty("order_id", out var orderProp) ? orderProp.GetString() : null;
            var uuid = root.TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
            var amountStr = root.TryGetProperty("payment_amount", out var amtProp) ? amtProp.GetString() : "0";
            var amount = decimal.TryParse(amountStr, out var a) ? a : 0;

            var eventType = status switch
            {
                "paid" or "paid_over" => WebhookEventType.PaymentCompleted,
                "fail" or "cancel" or "system_fail" or "wrong_amount" => WebhookEventType.PaymentFailed,
                "refund_paid" => WebhookEventType.PaymentRefunded,
                _ => WebhookEventType.Other
            };

            return Task.FromResult(new WebhookResult
            {
                Success = true,
                TransactionId = uuid,
                InvoiceId = orderId,
                EventType = eventType,
                Amount = amount
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
        var sign = ComputeSign(json);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("merchant", _merchantId);
        request.Headers.Add("sign", sign);

        return await CreateClient().SendAsync(request);
    }

    /// <summary>
    /// Computes the Cryptomus request signature. The provider mandates an MD5
    /// hash over <c>base64(body) + apiKey</c>. MD5 is weak in general but is
    /// required by the vendor protocol; it is not used for our own integrity
    /// or authentication outside of this contract.
    /// </summary>
    private string ComputeSign(string jsonBody)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonBody));
        var input = base64 + _apiKey;
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Performs a constant-time comparison of two hex strings to mitigate
    /// timing side channels when validating provider signatures.
    /// </summary>
    private static bool FixedTimeHexEquals(string expectedHex, string receivedHex)
    {
        if (expectedHex.Length != receivedHex.Length) return false;

        byte[] a;
        byte[] b;
        try
        {
            a = Convert.FromHexString(expectedHex);
            b = Convert.FromHexString(receivedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
