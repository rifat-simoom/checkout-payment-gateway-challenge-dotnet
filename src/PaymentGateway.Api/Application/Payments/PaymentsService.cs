using Microsoft.Extensions.Logging;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments;

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

        var payment = Payment.CreatePending(
            Guid.NewGuid(),
            command.CardNumber,
            command.ExpiryMonth,
            command.ExpiryYear,
            command.Currency,
            command.Amount);

        await _paymentsRepository.AddAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} created with status {Status}.",
            payment.Id,
            payment.Status);

        var bankResult = await _acquiringBankClient.ProcessAsync(
            new AcquiringBankPaymentRequest(
                command.CardNumber,
                command.ExpiryMonth,
                command.ExpiryYear,
                command.Currency,
                command.Amount,
                command.Cvv),
            cancellationToken);

        if (bankResult.Authorized)
        {
            payment.Authorize();
        }
        else
        {
            payment.Decline();
        }

        await _paymentsRepository.UpdateAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} processed with status {Status}.",
            payment.Id,
            payment.Status);

        return payment.Status == PaymentStatus.Authorized
            ? ProcessPaymentResult.Authorized(payment)
            : ProcessPaymentResult.Declined(payment);
    }

    public async Task<GetPaymentResult?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _paymentsRepository.GetAsync(paymentId, cancellationToken);

        if (payment is null)
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
}
