using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.PayPal;

public class PayPalGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient = new();
    private string _clientId = string.Empty;
    private string _clientSecret = string.Empty;
    private bool _sandbox = true;
    private string _baseUrl => _sandbox
        ? "https://api-m.sandbox.paypal.com"
        : "https://api-m.paypal.com";

    public string Name => "paypal";
    public string DisplayName => "PayPal";
    public string Version => "1.0.0";
    public string? Description => "PayPal payment gateway integration using REST API v2";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _clientId = settings.GetValueOrDefault("ClientId", string.Empty);
        _clientSecret = settings.GetValueOrDefault("ClientSecret", string.Empty);
        _sandbox = settings.GetValueOrDefault("Sandbox", "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["ClientId"] = "",
            ["ClientSecret"] = "",
            ["Sandbox"] = "true"
        };
    }

    public async Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "Failed to obtain PayPal access token",
                    AmountPaid = 0
                };
            }

            var orderPayload = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = request.InvoiceId,
                        description = string.IsNullOrEmpty(request.Description)
                            ? $"Invoice {request.InvoiceId}"
                            : request.Description,
                        amount = new
                        {
                            currency_code = request.Currency,
                            value = request.Amount.ToString("F2")
                        }
                    }
                },
                application_context = new
                {
                    return_url = request.ReturnUrl,
                    cancel_url = request.CancelUrl,
                    brand_name = "OpenGate"
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/checkout/orders");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(orderPayload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = $"PayPal API error: {content}",
                    AmountPaid = 0
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var orderId = root.GetProperty("id").GetString() ?? string.Empty;
            var links = root.GetProperty("links");
            string? approvalUrl = null;

            foreach (var link in links.EnumerateArray())
            {
                if (link.GetProperty("rel").GetString() == "approve")
                {
                    approvalUrl = link.GetProperty("href").GetString();
                    break;
                }
            }

            return new PaymentResult
            {
                Success = true,
                TransactionId = orderId,
                PaymentUrl = approvalUrl ?? string.Empty,
                AmountPaid = 0,
                Metadata = new Dictionary<string, string>
                {
                    ["OrderId"] = orderId
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                AmountPaid = 0
            };
        }
    }

    public async Task<PaymentResult> VerifyPaymentAsync(string transactionId)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = "Failed to obtain PayPal access token",
                    AmountPaid = 0
                };
            }

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v2/checkout/orders/{transactionId}");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(requestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"PayPal API error: {content}",
                    AmountPaid = 0
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString() ?? string.Empty;

            if (status == "APPROVED")
            {
                var captureResult = await CaptureOrderAsync(accessToken, transactionId);
                return captureResult;
            }

            if (status != "COMPLETED")
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"Order status: {status}",
                    AmountPaid = 0
                };
            }

            decimal amountPaid = 0;
            if (root.TryGetProperty("purchase_units", out var purchaseUnits) && purchaseUnits.GetArrayLength() > 0)
            {
                var firstUnit = purchaseUnits[0];
                if (firstUnit.TryGetProperty("amount", out var amountObj) &&
                    amountObj.TryGetProperty("value", out var valueProp))
                {
                    amountPaid = decimal.Parse(valueProp.GetString() ?? "0");
                }
            }

            return new PaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                AmountPaid = amountPaid,
                Metadata = new Dictionary<string, string>
                {
                    ["Status"] = status
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = ex.Message,
                AmountPaid = 0
            };
        }
    }

    public async Task<PaymentResult> RefundAsync(string transactionId, decimal amount)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = "Failed to obtain PayPal access token",
                    AmountPaid = 0
                };
            }

            var captureId = transactionId;
            if (!transactionId.StartsWith("capture_", StringComparison.OrdinalIgnoreCase))
            {
                var captureIdResult = await GetCaptureIdFromOrderAsync(accessToken, transactionId);
                if (string.IsNullOrEmpty(captureIdResult))
                {
                    return new PaymentResult
                    {
                        Success = false,
                        TransactionId = transactionId,
                        ErrorMessage = "Could not find capture ID for order. Order may need to be captured first.",
                        AmountPaid = 0
                    };
                }
                captureId = captureIdResult;
            }

            var refundPayload = new
            {
                amount = new
                {
                    value = amount.ToString("F2"),
                    currency_code = "USD"
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/payments/captures/{captureId}/refund");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(refundPayload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = $"PayPal refund error: {content}",
                    AmountPaid = 0
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var refundId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;

            return new PaymentResult
            {
                Success = true,
                TransactionId = refundId,
                AmountPaid = amount,
                Metadata = new Dictionary<string, string>
                {
                    ["RefundId"] = refundId,
                    ["CaptureId"] = captureId
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = ex.Message,
                AmountPaid = 0
            };
        }
    }

    public async Task<string> GetPaymentUrl(PaymentRequest request)
    {
        var result = await CreatePaymentAsync(request);
        return result.PaymentUrl ?? string.Empty;
    }

    public async Task<WebhookResult> HandleWebhookAsync(string payload, Dictionary<string, string> headers)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("event_type", out var eventTypeProp)
                ? eventTypeProp.GetString() ?? string.Empty
                : string.Empty;

            if (eventType != "PAYMENT.CAPTURE.COMPLETED" && eventType != "PAYMENT.CAPTURE.DENIED")
            {
                return new WebhookResult
                {
                    Success = true,
                    EventType = WebhookEventType.Other
                };
            }

            string? captureId = null;
            decimal amount = 0;
            string? invoiceId = null;

            if (root.TryGetProperty("resource", out var resource))
            {
                if (resource.TryGetProperty("id", out var idProp))
                    captureId = idProp.GetString();
                if (resource.TryGetProperty("amount", out var amountObj) &&
                    amountObj.TryGetProperty("value", out var valueProp))
                    amount = decimal.Parse(valueProp.GetString() ?? "0");
                if (resource.TryGetProperty("supplementary_data", out var suppData) &&
                    suppData.TryGetProperty("related_ids", out var relatedIds) &&
                    relatedIds.TryGetProperty("order_id", out var orderIdProp))
                {
                    invoiceId = orderIdProp.GetString();
                }
            }

            var eventTypeResult = eventType == "PAYMENT.CAPTURE.COMPLETED"
                ? WebhookEventType.PaymentCompleted
                : WebhookEventType.PaymentFailed;

            return new WebhookResult
            {
                Success = true,
                TransactionId = captureId,
                InvoiceId = invoiceId,
                EventType = eventTypeResult,
                Amount = amount
            };
        }
        catch (Exception)
        {
            return new WebhookResult
            {
                Success = false,
                EventType = WebhookEventType.Other
            };
        }
    }

    private async Task<PaymentResult> CaptureOrderAsync(string accessToken, string orderId)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/checkout/orders/{orderId}/capture");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = orderId,
                    ErrorMessage = $"PayPal capture error: {content}",
                    AmountPaid = 0
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString() ?? string.Empty;

            if (status != "COMPLETED")
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = orderId,
                    ErrorMessage = $"Capture status: {status}",
                    AmountPaid = 0
                };
            }

            decimal amountPaid = 0;
            if (root.TryGetProperty("purchase_units", out var purchaseUnits) && purchaseUnits.GetArrayLength() > 0)
            {
                var firstUnit = purchaseUnits[0];
                if (firstUnit.TryGetProperty("payments", out var payments) &&
                    payments.TryGetProperty("captures", out var captures) &&
                    captures.GetArrayLength() > 0)
                {
                    var firstCapture = captures[0];
                    if (firstCapture.TryGetProperty("amount", out var amountObj) &&
                        amountObj.TryGetProperty("value", out var valueProp))
                    {
                        amountPaid = decimal.Parse(valueProp.GetString() ?? "0");
                    }
                }
            }

            return new PaymentResult
            {
                Success = true,
                TransactionId = orderId,
                AmountPaid = amountPaid,
                Metadata = new Dictionary<string, string> { ["Status"] = status }
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = orderId,
                ErrorMessage = ex.Message,
                AmountPaid = 0
            };
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            requestMessage.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(requestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            return root.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetCaptureIdFromOrderAsync(string accessToken, string orderId)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v2/checkout/orders/{orderId}");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(requestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.TryGetProperty("purchase_units", out var purchaseUnits) && purchaseUnits.GetArrayLength() > 0)
            {
                var firstUnit = purchaseUnits[0];
                if (firstUnit.TryGetProperty("payments", out var payments) &&
                    payments.TryGetProperty("captures", out var captures) &&
                    captures.GetArrayLength() > 0)
                {
                    var firstCapture = captures[0];
                    return firstCapture.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
