using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments.Models;

public sealed record ProcessPaymentResult(
    Guid? Id,
    PaymentStatus Status,
    string? LastFourCardDigits,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? Currency,
    int? Amount,
    IReadOnlyCollection<PaymentValidationError> Errors,
    bool IsNewPayment)
{
    public static ProcessPaymentResult Authorized(Payment payment, bool isNewPayment = true) =>
        FromPayment(payment, isNewPayment);

    public static ProcessPaymentResult Declined(Payment payment, bool isNewPayment = true) =>
        FromPayment(payment, isNewPayment);

    public static ProcessPaymentResult Pending(Payment payment, bool isNewPayment = false) =>
        FromPayment(payment, isNewPayment);

    public static ProcessPaymentResult Rejected(IReadOnlyCollection<PaymentValidationError> errors) =>
        new(null, PaymentStatus.Rejected, null, null, null, null, null, errors, false);

    private static ProcessPaymentResult FromPayment(Payment payment, bool isNewPayment) =>
        new(
            payment.Id,
            payment.Status,
            payment.LastFourCardDigits,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount,
            Array.Empty<PaymentValidationError>(),
            isNewPayment);
}
