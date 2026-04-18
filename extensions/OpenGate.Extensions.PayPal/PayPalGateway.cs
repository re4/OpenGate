using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.PayPal;

/// <summary>
/// PayPal REST API v2 payment gateway. Uses Checkout Orders for payments and
/// the official Notifications API for webhook signature verification. The
/// gateway propagates the merchant invoice id via the purchase unit
/// <c>custom_id</c> field so it surfaces directly on capture events.
/// </summary>
public class PayPalGateway : IPaymentGateway
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly Lazy<HttpClient> _fallbackClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
    private string _clientId = string.Empty;
    private string _clientSecret = string.Empty;
    private string _webhookId = string.Empty;
    private bool _sandbox = true;

    /// <summary>
    /// Initializes a new <see cref="PayPalGateway"/>. The
    /// <see cref="IHttpClientFactory"/> is optional so the gateway can still
    /// be instantiated outside the host (e.g. background utilities).
    /// </summary>
    public PayPalGateway(IServiceProvider? serviceProvider = null)
    {
        _httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
    }

    private HttpClient HttpClient => _httpClientFactory?.CreateClient("OpenGate.Default") ?? _fallbackClient.Value;

    private string BaseUrl => _sandbox
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
        _webhookId = settings.GetValueOrDefault("WebhookId", string.Empty);
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
            ["WebhookId"] = "",
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
                        custom_id = request.InvoiceId,
                        invoice_id = request.InvoiceId,
                        description = string.IsNullOrEmpty(request.Description)
                            ? $"Invoice {request.InvoiceId}"
                            : request.Description,
                        amount = new
                        {
                            currency_code = request.Currency,
                            value = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
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

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(orderPayload),
                Encoding.UTF8,
                "application/json");

            var response = await HttpClient.SendAsync(requestMessage);
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
            string? approvalUrl = null;

            if (root.TryGetProperty("links", out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("rel", out var relProp) && relProp.GetString() == "approve")
                    {
                        approvalUrl = link.TryGetProperty("href", out var hrefProp) ? hrefProp.GetString() : null;
                        break;
                    }
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
        catch (Exception)
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred. Please try again.",
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

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v2/checkout/orders/{transactionId}");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await HttpClient.SendAsync(requestMessage);
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
                    decimal.TryParse(valueProp.GetString() ?? "0", System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out amountPaid);
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
        catch (Exception)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = "An unexpected error occurred. Please try again.",
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
                    value = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    currency_code = "USD"
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/payments/captures/{captureId}/refund");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(refundPayload),
                Encoding.UTF8,
                "application/json");

            var response = await HttpClient.SendAsync(requestMessage);
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
        catch (Exception)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = "An unexpected error occurred. Please try again.",
                AmountPaid = 0
            };
        }
    }

    public async Task<string> GetPaymentUrl(PaymentRequest request)
    {
        var result = await CreatePaymentAsync(request);
        return result.PaymentUrl ?? string.Empty;
    }

    /// <summary>
    /// Verifies the inbound webhook against PayPal's
    /// <c>/v1/notifications/verify-webhook-signature</c> endpoint and only
    /// then projects the event onto a <see cref="WebhookResult"/>. The
    /// merchant invoice id is read from the capture resource's
    /// <c>custom_id</c> (set when the order was created).
    /// </summary>
    public async Task<WebhookResult> HandleWebhookAsync(string payload, Dictionary<string, string> headers)
    {
        try
        {
            if (string.IsNullOrEmpty(_webhookId) || string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                return new WebhookResult { Success = false, EventType = WebhookEventType.Other };
            }

            var verified = await VerifyWebhookSignatureAsync(payload, headers);
            if (!verified)
            {
                return new WebhookResult { Success = false, EventType = WebhookEventType.Other };
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("event_type", out var eventTypeProp)
                ? eventTypeProp.GetString() ?? string.Empty
                : string.Empty;

            var mappedEvent = eventType switch
            {
                "PAYMENT.CAPTURE.COMPLETED" => WebhookEventType.PaymentCompleted,
                "PAYMENT.CAPTURE.DENIED" or "PAYMENT.CAPTURE.DECLINED" => WebhookEventType.PaymentFailed,
                "PAYMENT.CAPTURE.REFUNDED" or "PAYMENT.CAPTURE.REVERSED" => WebhookEventType.PaymentRefunded,
                _ => WebhookEventType.Other
            };

            if (mappedEvent == WebhookEventType.Other)
            {
                return new WebhookResult { Success = true, EventType = WebhookEventType.Other };
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
                {
                    decimal.TryParse(valueProp.GetString() ?? "0", System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out amount);
                }
                if (resource.TryGetProperty("custom_id", out var customProp))
                    invoiceId = customProp.GetString();
                else if (resource.TryGetProperty("invoice_id", out var invoiceProp))
                    invoiceId = invoiceProp.GetString();
            }

            return new WebhookResult
            {
                Success = true,
                TransactionId = captureId,
                InvoiceId = invoiceId,
                EventType = mappedEvent,
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

    private async Task<bool> VerifyWebhookSignatureAsync(string payload, Dictionary<string, string> headers)
    {
        var headerLookup = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);

        if (!headerLookup.TryGetValue("PAYPAL-AUTH-ALGO", out var authAlgo)
            || !headerLookup.TryGetValue("PAYPAL-CERT-URL", out var certUrl)
            || !headerLookup.TryGetValue("PAYPAL-TRANSMISSION-ID", out var transmissionId)
            || !headerLookup.TryGetValue("PAYPAL-TRANSMISSION-SIG", out var transmissionSig)
            || !headerLookup.TryGetValue("PAYPAL-TRANSMISSION-TIME", out var transmissionTime))
        {
            return false;
        }

        var accessToken = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
            return false;

        using var verificationDoc = JsonDocument.Parse(payload);

        var verificationPayload = new Dictionary<string, object>
        {
            ["auth_algo"] = authAlgo,
            ["cert_url"] = certUrl,
            ["transmission_id"] = transmissionId,
            ["transmission_sig"] = transmissionSig,
            ["transmission_time"] = transmissionTime,
            ["webhook_id"] = _webhookId,
            ["webhook_event"] = JsonSerializer.Deserialize<JsonElement>(payload)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/notifications/verify-webhook-signature");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(verificationPayload), Encoding.UTF8, "application/json");

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return false;

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("verification_status", out var statusProp))
            return false;

        return string.Equals(statusProp.GetString(), "SUCCESS", StringComparison.Ordinal);
    }

    private async Task<PaymentResult> CaptureOrderAsync(string accessToken, string orderId)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders/{orderId}/capture");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(requestMessage);
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
                        decimal.TryParse(valueProp.GetString() ?? "0", System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out amountPaid);
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
        catch (Exception)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = orderId,
                ErrorMessage = "An unexpected error occurred. Please try again.",
                AmountPaid = 0
            };
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            requestMessage.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await HttpClient.SendAsync(requestMessage);
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
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v2/checkout/orders/{orderId}");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await HttpClient.SendAsync(requestMessage);
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
