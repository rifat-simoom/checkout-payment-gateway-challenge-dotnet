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
        : this(id, status, string.Empty, string.Empty, string.Empty, false, lastFourCardDigits, expiryMonth, expiryYear, currency, amount)
    {
    }

    public Payment(
        Guid id,
        PaymentStatus status,
        string merchantId,
        string idempotencyKey,
        string requestFingerprint,
        bool isProcessing,
        string lastFourCardDigits,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount)
    {
        Id = id;
        Status = status;
        MerchantId = merchantId;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        IsProcessing = isProcessing;
        LastFourCardDigits = lastFourCardDigits;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
    }

    public Guid Id { get; }

    public PaymentStatus Status { get; private set; }

    public string MerchantId { get; }

    public string IdempotencyKey { get; }

    public string RequestFingerprint { get; }

    public bool IsProcessing { get; private set; }

    public string LastFourCardDigits { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    public int Amount { get; }

    public static Payment CreatePending(
        Guid id,
        string merchantId,
        string idempotencyKey,
        string requestFingerprint,
        string cardNumber,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount) =>
        new(
            id,
            PaymentStatus.Pending,
            merchantId,
            idempotencyKey,
            requestFingerprint,
            true,
            cardNumber[^4..],
            expiryMonth,
            expiryYear,
            currency,
            amount);

    public void Authorize() => Complete(PaymentStatus.Authorized);

    public void Decline() => Complete(PaymentStatus.Declined);

    public void MarkProcessing()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payments can be marked as processing.");
        }

        IsProcessing = true;
    }

    public void MarkProcessingFailed()
    {
        if (Status == PaymentStatus.Pending)
        {
            IsProcessing = false;
        }
    }

    private void Complete(PaymentStatus status)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payments can be completed.");
        }

        Status = status;
        IsProcessing = false;
    }
}