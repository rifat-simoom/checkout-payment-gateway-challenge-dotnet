using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments;

public sealed record GetPaymentResult(
    Guid Id,
    PaymentStatus Status,
    string LastFourCardDigits,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount)
{
    public static GetPaymentResult FromPayment(Payment payment) =>
        new(
            payment.Id,
            payment.Status,
            payment.LastFourCardDigits,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount);
}
