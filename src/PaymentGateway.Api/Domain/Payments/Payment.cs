namespace PaymentGateway.Api.Domain.Payments;

public sealed record Payment(
    Guid Id,
    PaymentStatus Status,
    string LastFourCardDigits,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount);
