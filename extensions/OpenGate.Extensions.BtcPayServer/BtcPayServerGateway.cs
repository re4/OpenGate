using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.BtcPayServer;

public class BtcPayServerGateway : IPaymentGateway
{
    private HttpClient _httpClient = new();
    private string _apiKey = string.Empty;
    private string _storeId = string.Empty;
    private string _webhookSecret = string.Empty;

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
        if (!string.IsNullOrEmpty(serverUrl))
            _httpClient = new HttpClient { BaseAddress = new Uri(serverUrl + "/") };

        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["ServerUrl"] = "https://btcpay.example.com",
            ["ApiKey"] = "",
            ["StoreId"] = "",
            ["WebhookSecret"] = ""
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

            var response = await _httpClient.SendAsync(httpRequest);
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
            return new PaymentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<PaymentResult> VerifyPaymentAsync(string transactionId)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/stores/{_storeId}/invoices/{transactionId}");
            httpRequest.Headers.Add("Authorization", $"token {_apiKey}");

            var response = await _httpClient.SendAsync(httpRequest);
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
            return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = ex.Message };
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

            var response = await _httpClient.SendAsync(httpRequest);
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
            return new PaymentResult { Success = false, TransactionId = transactionId, ErrorMessage = ex.Message };
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
            if (!string.IsNullOrEmpty(_webhookSecret))
            {
                var receivedSig = headers.GetValueOrDefault("BTCPay-Sig", "");
                if (!string.IsNullOrEmpty(receivedSig))
                {
                    var sigValue = receivedSig.StartsWith("sha256=")
                        ? receivedSig["sha256=".Length..]
                        : receivedSig;

                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret));
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                    var expectedSig = Convert.ToHexStringLower(hash);

                    if (!string.Equals(expectedSig, sigValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(new WebhookResult { Success = false, EventType = WebhookEventType.Other });
                    }
                }
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
