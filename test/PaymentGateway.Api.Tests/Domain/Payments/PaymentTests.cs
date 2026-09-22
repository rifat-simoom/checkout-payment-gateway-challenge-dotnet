using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests.Domain.Payments;

public sealed class PaymentTests
{
    [Fact]
    public void CreatePending_CreatesPendingPaymentWithSafeCardDetails()
    {
        var paymentId = Guid.NewGuid();

        var payment = Payment.CreatePending(paymentId, "2222405343248877", 12, 2030, "GBP", 100);

        Assert.Equal(paymentId, payment.Id);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal("8877", payment.LastFourCardDigits);
        Assert.Equal(12, payment.ExpiryMonth);
        Assert.Equal(2030, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(100, payment.Amount);
    }

    [Fact]
    public void Authorize_UpdatesPaymentStatusToAuthorized()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), "2222405343248877", 12, 2030, "GBP", 100);

        payment.Authorize();

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public void Decline_UpdatesPaymentStatusToDeclined()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), "2222405343248878", 12, 2030, "GBP", 100);

        payment.Decline();

        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Fact]
    public void Authorize_Throws_WhenPaymentIsNotPending()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), "2222405343248877", 12, 2030, "GBP", 100);
        payment.Authorize();

        Assert.Throws<InvalidOperationException>(() => payment.Authorize());
    }
}
