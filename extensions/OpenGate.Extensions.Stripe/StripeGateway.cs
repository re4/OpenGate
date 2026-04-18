using OpenGate.Extensions.Abstractions;
using Stripe;
using Stripe.Checkout;

namespace OpenGate.Extensions.Stripe;

public class StripeGateway : IPaymentGateway
{
    private string _secretKey = string.Empty;
    private string _publishableKey = string.Empty;
    private string _webhookSecret = string.Empty;

    public string Name => "stripe";
    public string DisplayName => "Stripe";
    public string Version => "1.0.0";
    public string? Description => "Stripe payment gateway integration using Checkout Sessions";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _secretKey = settings.GetValueOrDefault("SecretKey", string.Empty);
        _publishableKey = settings.GetValueOrDefault("PublishableKey", string.Empty);
        _webhookSecret = settings.GetValueOrDefault("WebhookSecret", string.Empty);
        StripeConfiguration.ApiKey = _secretKey;
        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["SecretKey"] = "sk_test_...",
            ["PublishableKey"] = "pk_test_...",
            ["WebhookSecret"] = "whsec_..."
        };
    }

    public async Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(CreateSessionOptions(request));

            return new PaymentResult
            {
                Success = true,
                TransactionId = session.Id,
                PaymentUrl = session.Url,
                AmountPaid = 0,
                Metadata = new Dictionary<string, string>
                {
                    ["SessionId"] = session.Id
                }
            };
        }
        catch (StripeException ex)
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred. Please try again.",
                AmountPaid = 0
            };
        }
        catch (Exception ex)
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
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(transactionId);

            if (session.PaymentStatus == "paid")
            {
                var amountPaid = session.AmountTotal.HasValue
                    ? session.AmountTotal.Value / 100m
                    : 0m;

                return new PaymentResult
                {
                    Success = true,
                    TransactionId = session.Id,
                    AmountPaid = amountPaid,
                    Metadata = new Dictionary<string, string>
                    {
                        ["PaymentIntentId"] = session.PaymentIntentId ?? string.Empty,
                        ["ClientReferenceId"] = session.ClientReferenceId ?? string.Empty
                    }
                };
            }

            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = $"Payment status: {session.PaymentStatus}",
                AmountPaid = 0
            };
        }
        catch (StripeException ex)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = "An unexpected error occurred. Please try again.",
                AmountPaid = 0
            };
        }
        catch (Exception ex)
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
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(transactionId);

            if (string.IsNullOrEmpty(session.PaymentIntentId))
            {
                return new PaymentResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = "No payment intent found for this session",
                    AmountPaid = 0
                };
            }

            var refundService = new RefundService();
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = session.PaymentIntentId,
                Amount = amount > 0 ? (long)(amount * 100) : null,
                Reason = "requested_by_customer"
            };

            var refund = await refundService.CreateAsync(refundOptions);

            return new PaymentResult
            {
                Success = refund.Status == "succeeded" || refund.Status == "pending",
                TransactionId = refund.Id,
                AmountPaid = amount,
                Metadata = new Dictionary<string, string>
                {
                    ["RefundId"] = refund.Id,
                    ["RefundStatus"] = refund.Status ?? string.Empty
                }
            };
        }
        catch (StripeException ex)
        {
            return new PaymentResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = "An unexpected error occurred. Please try again.",
                AmountPaid = 0
            };
        }
        catch (Exception ex)
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

    public async Task<WebhookResult> HandleWebhookAsync(string payload, Dictionary<string, string> headers)
    {
        try
        {
            if (string.IsNullOrEmpty(_webhookSecret))
            {
                return new WebhookResult
                {
                    Success = false,
                    EventType = WebhookEventType.Other
                };
            }

            var signature = headers.GetValueOrDefault("Stripe-Signature", string.Empty);
            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);
            }
            catch (StripeException)
            {
                return new WebhookResult
                {
                    Success = false,
                    EventType = WebhookEventType.Other
                };
            }

            return stripeEvent.Type switch
            {
                "checkout.session.completed" => await HandleCheckoutSessionCompletedAsync(stripeEvent),
                "payment_intent.payment_failed" => HandlePaymentIntentFailed(stripeEvent),
                "charge.refunded" => HandleChargeRefunded(stripeEvent),
                _ => new WebhookResult
                {
                    Success = true,
                    EventType = WebhookEventType.Other
                }
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

    /// <summary>
    /// Builds a Stripe Checkout session for the supplied payment request and
    /// propagates the merchant invoice id into both the session metadata and
    /// the PaymentIntent metadata so that downstream events (refunds, etc.)
    /// can resolve the originating invoice without ambiguity.
    /// </summary>
    private static SessionCreateOptions CreateSessionOptions(PaymentRequest request)
    {
        var metadata = new Dictionary<string, string>(request.Metadata ?? new Dictionary<string, string>())
        {
            ["InvoiceId"] = request.InvoiceId
        };

        return new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = request.ReturnUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = request.CancelUrl,
            ClientReferenceId = request.InvoiceId,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmountDecimal = request.Amount * 100m,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrEmpty(request.Description)
                                ? $"Invoice {request.InvoiceId}"
                                : request.Description
                        }
                    },
                    Quantity = 1
                }
            }
        };
    }

    private static Task<WebhookResult> HandleCheckoutSessionCompletedAsync(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        if (session == null)
        {
            return Task.FromResult(new WebhookResult
            {
                Success = false,
                EventType = WebhookEventType.Other
            });
        }

        var amount = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : 0m;

        return Task.FromResult(new WebhookResult
        {
            Success = true,
            TransactionId = session.Id,
            InvoiceId = session.ClientReferenceId,
            EventType = WebhookEventType.PaymentCompleted,
            Amount = amount
        });
    }

    private static WebhookResult HandlePaymentIntentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
        {
            return new WebhookResult
            {
                Success = false,
                EventType = WebhookEventType.Other
            };
        }

        return new WebhookResult
        {
            Success = true,
            TransactionId = paymentIntent.Id,
            EventType = WebhookEventType.PaymentFailed,
            Amount = paymentIntent.Amount / 100m
        };
    }

    /// <summary>
    /// Handles a Stripe charge.refunded webhook by extracting the original
    /// merchant invoice id from charge / payment intent metadata so the
    /// caller can mark the right invoice as refunded. The original invoice
    /// id is the <c>ClientReferenceId</c> we set when creating the Checkout
    /// session, propagated through metadata or the linked PaymentIntent.
    /// </summary>
    private static WebhookResult HandleChargeRefunded(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Charge charge)
        {
            return new WebhookResult
            {
                Success = false,
                EventType = WebhookEventType.Other
            };
        }

        string? invoiceId = null;
        if (charge.Metadata != null && charge.Metadata.TryGetValue("InvoiceId", out var fromCharge) && !string.IsNullOrEmpty(fromCharge))
        {
            invoiceId = fromCharge;
        }
        else if (charge.PaymentIntent?.Metadata != null && charge.PaymentIntent.Metadata.TryGetValue("InvoiceId", out var fromIntent) && !string.IsNullOrEmpty(fromIntent))
        {
            invoiceId = fromIntent;
        }

        return new WebhookResult
        {
            Success = true,
            TransactionId = charge.PaymentIntentId ?? charge.Id,
            InvoiceId = invoiceId,
            EventType = WebhookEventType.PaymentRefunded,
            Amount = charge.AmountRefunded / 100m
        };
    }
}
