namespace PaymentGateway.Api.Domain.Payments;

public sealed class Payment
{
    public Payment(
        Guid id,
        PaymentStatus status,
        string lastFourCardDigits,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount)
    {
        Id = id;
        Status = status;
        LastFourCardDigits = lastFourCardDigits;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
    }

    public Guid Id { get; }

    public PaymentStatus Status { get; private set; }

    public string LastFourCardDigits { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    public int Amount { get; }

    public static Payment CreatePending(
        Guid id,
        string cardNumber,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount) =>
        new(
            id,
            PaymentStatus.Pending,
            cardNumber[^4..],
            expiryMonth,
            expiryYear,
            currency,
            amount);

    public void Authorize() => Complete(PaymentStatus.Authorized);

    public void Decline() => Complete(PaymentStatus.Declined);

    private void Complete(PaymentStatus status)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payments can be completed.");
        }

        Status = status;
    }
}
