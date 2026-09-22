using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests.Domain.Payments;

public sealed class PaymentTests
{
    [Fact]
    public void CreatePending_CreatesPendingPaymentWithSafeCardDetails()
    {
        var paymentId = Guid.NewGuid();

        var payment = Payment.CreatePending(
            paymentId,
            "merchant-001",
            "invoice-001",
            "fingerprint-001",
            "2222405343248877",
            12,
            2030,
            "GBP",
            100);

        Assert.Equal(paymentId, payment.Id);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.True(payment.IsProcessing);
        Assert.Equal("merchant-001", payment.MerchantId);
        Assert.Equal("invoice-001", payment.IdempotencyKey);
        Assert.Equal("fingerprint-001", payment.RequestFingerprint);
        Assert.Equal("8877", payment.LastFourCardDigits);
        Assert.Equal(12, payment.ExpiryMonth);
        Assert.Equal(2030, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(100, payment.Amount);
    }

    [Fact]
    public void Authorize_UpdatesPaymentStatusToAuthorized()
    {
        var payment = CreatePendingPayment();

        payment.Authorize();

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.False(payment.IsProcessing);
    }

    [Fact]
    public void Decline_UpdatesPaymentStatusToDeclined()
    {
        var payment = CreatePendingPayment();

        payment.Decline();

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.False(payment.IsProcessing);
    }

    [Fact]
    public void MarkProcessingFailed_MakesPendingPaymentRetryable()
    {
        var payment = CreatePendingPayment();

        payment.MarkProcessingFailed();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(payment.IsProcessing);
    }

    [Fact]
    public void MarkProcessing_MarksPendingPaymentAsProcessing()
    {
        var payment = CreatePendingPayment();
        payment.MarkProcessingFailed();

        payment.MarkProcessing();

        Assert.True(payment.IsProcessing);
    }

    [Fact]
    public void Authorize_Throws_WhenPaymentIsNotPending()
    {
        var payment = CreatePendingPayment();
        payment.Authorize();

        Assert.Throws<InvalidOperationException>(() => payment.Authorize());
    }

    private static Payment CreatePendingPayment() =>
        Payment.CreatePending(
            Guid.NewGuid(),
            "merchant-001",
            "invoice-001",
            "fingerprint-001",
            "2222405343248877",
            12,
            2030,
            "GBP",
            100);
}