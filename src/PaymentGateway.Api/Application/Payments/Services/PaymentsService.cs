using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Application.Payments.Exceptions;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Application.Payments.Validators;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments.Services;

public sealed class PaymentsService : IPaymentsService
{
    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly ILogger<PaymentsService> _logger;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly PaymentRequestValidator _paymentRequestValidator;

    public PaymentsService(
        IAcquiringBankClient acquiringBankClient,
        IPaymentsRepository paymentsRepository,
        ILogger<PaymentsService> logger)
    {
        _acquiringBankClient = acquiringBankClient;
        _logger = logger;
        _paymentsRepository = paymentsRepository;
        _paymentRequestValidator = new PaymentRequestValidator();
    }

    public async Task<ProcessPaymentResult> ProcessAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Payment processing started for currency {Currency} and amount {Amount}.",
            command.Currency,
            command.Amount);

        var validationErrors = _paymentRequestValidator.Validate(command);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning(
                "Payment request rejected with {ErrorCount} validation errors: {ValidationErrorCodes}.",
                validationErrors.Count,
                validationErrors.Select(error => error.Code).ToArray());

            return ProcessPaymentResult.Rejected(validationErrors);
        }

        var currency = Normalize(command.Currency);

        var payment = Payment.CreatePending(
            Guid.NewGuid(),
            Normalize(command.MerchantId),
            Normalize(command.IdempotencyKey),
            CreateRequestFingerprint(command),
            command.CardNumber,
            command.ExpiryMonth,
            command.ExpiryYear,
            currency,
            command.Amount);

        var startResult = await _paymentsRepository.StartAsync(payment, cancellationToken);
        if (startResult.Status == PaymentStartStatus.Conflict)
        {
            _logger.LogWarning(
                "Idempotency key conflict for merchant {MerchantId}.",
                Normalize(command.MerchantId));

            throw new IdempotencyKeyConflictException();
        }

        if (startResult.Status == PaymentStartStatus.Existing)
        {
            _logger.LogInformation(
                "Payment {PaymentId} returned from idempotency replay with status {Status}.",
                startResult.Payment.Id,
                startResult.Payment.Status);

            return startResult.Payment.Status switch
            {
                PaymentStatus.Authorized => ProcessPaymentResult.Authorized(startResult.Payment, false),
                PaymentStatus.Declined => ProcessPaymentResult.Declined(startResult.Payment, false),
                PaymentStatus.Pending => ProcessPaymentResult.Pending(startResult.Payment),
                _ => throw new InvalidOperationException("Unexpected stored payment status.")
            };
        }

        payment = startResult.Payment;
        var isNewPayment = startResult.Status == PaymentStartStatus.Created;

        _logger.LogInformation(
            "Payment {PaymentId} created with status {Status}.",
            payment.Id,
            payment.Status);

        AcquiringBankPaymentResult bankResult;
        try
        {
            bankResult = await _acquiringBankClient.ProcessAsync(
                new AcquiringBankPaymentRequest(
                    command.CardNumber,
                    command.ExpiryMonth,
                    command.ExpiryYear,
                    currency,
                    command.Amount,
                    command.Cvv),
                cancellationToken);
        }
        catch (AcquiringBankUnavailableException)
        {
            await _paymentsRepository.MarkProcessingFailedAsync(payment.Id, CancellationToken.None);

            throw;
        }

        var completedPayment = await _paymentsRepository.CompleteAsync(
            payment.Id,
            bankResult.Authorized,
            cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} processed with status {Status}.",
            completedPayment.Id,
            completedPayment.Status);

        return completedPayment.Status == PaymentStatus.Authorized
            ? ProcessPaymentResult.Authorized(completedPayment, isNewPayment)
            : ProcessPaymentResult.Declined(completedPayment, isNewPayment);
    }

    public async Task<GetPaymentResult?> GetAsync(
        Guid paymentId,
        string merchantId,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentsRepository.GetAsync(paymentId, cancellationToken);

        if (payment is null || Normalize(payment.MerchantId) != Normalize(merchantId))
        {
            _logger.LogInformation("Payment {PaymentId} not found.", paymentId);

            return null;
        }

        _logger.LogInformation(
            "Payment {PaymentId} retrieved with status {Status}.",
            payment.Id,
            payment.Status);

        return GetPaymentResult.FromPayment(payment);
    }

    private static string CreateRequestFingerprint(ProcessPaymentCommand command)
    {
        var normalizedPaymentIntent = string.Join(
            "|",
            Normalize(command.CardNumber),
            command.ExpiryMonth.ToString("00"),
            command.ExpiryYear.ToString("0000"),
            Normalize(command.Currency),
            command.Amount.ToString());

        var fingerprintBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPaymentIntent));

        return Convert.ToHexString(fingerprintBytes);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}