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
    IReadOnlyCollection<PaymentValidationError> Errors)
{
    public static ProcessPaymentResult Authorized(Payment payment) => FromPayment(payment);

    public static ProcessPaymentResult Declined(Payment payment) => FromPayment(payment);

    public static ProcessPaymentResult Rejected(IReadOnlyCollection<PaymentValidationError> errors) =>
        new(null, PaymentStatus.Rejected, null, null, null, null, null, errors);

    private static ProcessPaymentResult FromPayment(Payment payment) =>
        new(
            payment.Id,
            payment.Status,
            payment.LastFourCardDigits,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount,
            Array.Empty<PaymentValidationError>());
}
