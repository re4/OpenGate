using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.BtcPayServer;

/// <summary>
/// BTCPay Server payment gateway using the Greenfield API. The configured
/// <c>ServerUrl</c> is validated against an SSRF allowlist so a compromised
/// admin account cannot redirect the gateway at internal infrastructure
/// unless <c>AllowPrivateHosts</c> is explicitly enabled.
/// </summary>
public class BtcPayServerGateway : IPaymentGateway
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly Lazy<HttpClient> _fallbackClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
    private Uri? _baseAddress;
    private string _apiKey = string.Empty;
    private string _storeId = string.Empty;
    private string _webhookSecret = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="BtcPayServerGateway"/>. The
    /// <see cref="IHttpClientFactory"/> is optional so the gateway can be
    /// instantiated outside the host (e.g. CLI utilities).
    /// </summary>
    public BtcPayServerGateway(IServiceProvider? serviceProvider = null)
    {
        _httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory?.CreateClient("OpenGate.Default") ?? _fallbackClient.Value;
        if (_baseAddress != null)
        {
            client.BaseAddress = _baseAddress;
        }
        return client;
    }

    public string Name => "btcpayserver";
    public string DisplayName => "BTCPay Server";
    public string Version => "1.0.0";
    public string? Description => "BTCPay Server self-hosted payment gateway integration via Greenfield API";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _apiKey = settings.GetValueOrDefault("ApiKey", string.Empty);
        _storeId = settings.GetValueOrDefault("StoreId", string.Empty);
        _webhookSecret = settings.GetValueOrDefault("WebhookSecret", string.Empty);

        var serverUrl = settings.GetValueOrDefault("ServerUrl", string.Empty).TrimEnd('/');
        var allowPrivate = bool.TryParse(settings.GetValueOrDefault("AllowPrivateHosts", "false"), out var allow) && allow;

        if (!string.IsNullOrEmpty(serverUrl)
            && HttpSecurity.TryValidateOutboundUrl(serverUrl + "/", allowPrivate, out var uri, out _)
            && uri != null)
        {
            _baseAddress = uri;
        }
        else
        {
            _baseAddress = null;
        }

        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["ServerUrl"] = "https://btcpay.example.com",
            ["ApiKey"] = "",
            ["StoreId"] = "",
            ["WebhookSecret"] = "",
            ["AllowPrivateHosts"] = "false"
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
                ["metadata"] = new Dictionary<string, string>
                {
                    ["orderId"] = request.InvoiceId
                },
                ["checkout"] = new Dictionary<string, object>
                {
                    ["redirectURL"] = request.ReturnUrl,
                    ["defaultLanguage"] = "en"
                }
            };

            if (!string.IsNullOrEmpty(request.Description))
            {
                var metadata = (Dictionary<string, string>)body["metadata"];
                metadata["itemDesc"] = request.Description;
            }

            var json = JsonSerializer.Serialize(body);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/v1/stores/{_storeId}/invoices")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("Authorization", $"token {_apiKey}");

            if (_baseAddress == null)
            {
                return new PaymentResult { Success = false, ErrorMessage = "BTCPay Server URL is not configured or not allowed." };
            }

            var response = await CreateClient().SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = $"BTCPay Server API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var checkoutLink = root.TryGetProperty("checkoutLink", out var linkProp) ? linkProp.GetString() : null;

            return new PaymentResult
            {
                Success = true,
                TransactionId = id,
                PaymentUrl = checkoutLink,
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/stores/{_storeId}/invoices/{transactionId}");
            httpRequest.Headers.Add("Authorization", $"token {_apiKey}");

            if (_baseAddress == null)
            {
                return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = "BTCPay Server URL is not configured or not allowed." };
            }

            var response = await CreateClient().SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"BTCPay Server API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() : "";
            var amountStr = root.TryGetProperty("amount", out var aProp) ? aProp.GetString() : "0";

            return new PaymentResult
            {
                Success = status is "Settled" or "Processing",
                TransactionId = transactionId,
                AmountPaid = decimal.TryParse(amountStr, out var amt) ? amt : 0,
                ErrorMessage = status is "Settled" or "Processing" ? null : $"Status: {status}"
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<PaymentResult> RefundAsync(string transactionId, decimal amount)
    {
        try
        {
            var body = new Dictionary<string, object>
            {
                ["name"] = $"Refund for {transactionId}",
                ["paymentMethod"] = "BTC",
                ["amount"] = amount.ToString("F2")
            };

            var json = JsonSerializer.Serialize(body);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/v1/stores/{_storeId}/invoices/{transactionId}/refund")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("Authorization", $"token {_apiKey}");

            if (_baseAddress == null)
            {
                return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = "BTCPay Server URL is not configured or not allowed." };
            }

            var response = await CreateClient().SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"BTCPay Server refund error ({(int)response.StatusCode}): {content}"
                };
            }

            return new PaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                AmountPaid = amount
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
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
            if (string.IsNullOrEmpty(_webhookSecret))
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });

            var receivedSig = headers.GetValueOrDefault("BTCPay-Sig", "");
            if (string.IsNullOrEmpty(receivedSig))
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });

            var sigValue = receivedSig.StartsWith("sha256=")
                ? receivedSig["sha256=".Length..]
                : receivedSig;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            byte[] received;
            try
            {
                received = Convert.FromHexString(sigValue);
            }
            catch (FormatException)
            {
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
            }

            if (!CryptographicOperations.FixedTimeEquals(expected, received))
            {
                return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "";
            var invoiceId = root.TryGetProperty("invoiceId", out var invProp) ? invProp.GetString() : null;

            string? orderId = null;
            if (root.TryGetProperty("metadata", out var metaProp) &&
                metaProp.TryGetProperty("orderId", out var oidProp))
            {
                orderId = oidProp.GetString();
            }

            var eventType = type switch
            {
                "InvoiceSettled" or "InvoicePaymentSettled" => WebhookEventType.PaymentCompleted,
                "InvoiceExpired" or "InvoiceInvalid" => WebhookEventType.PaymentFailed,
                _ => WebhookEventType.Other
            };

            decimal amount = 0;
            if (root.TryGetProperty("payment", out var paymentProp) &&
                paymentProp.TryGetProperty("value", out var valProp))
            {
                decimal.TryParse(valProp.GetString(), out amount);
            }

            return Task.FromResult(new WebhookResult
            {
                Success = true,
                TransactionId = invoiceId,
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
}
