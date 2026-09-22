using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Infrastructure.Persistence;

namespace PaymentGateway.Api.Tests.Infrastructure.Persistence;

public sealed class PaymentsRepositoryTests
{
    [Fact]
    public async Task AddAsync_StoresPayment()
    {
        var repository = new PaymentsRepository();
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Authorized, "8877", 12, 2030, "GBP", 100);

        await repository.AddAsync(payment, CancellationToken.None);

        var storedPayment = await repository.GetAsync(payment.Id, CancellationToken.None);
        Assert.Equal(payment, storedPayment);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesStoredPayment()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.AddAsync(payment, CancellationToken.None);

        payment.Authorize();
        await repository.UpdateAsync(payment, CancellationToken.None);

        var storedPayment = await repository.GetAsync(payment.Id, CancellationToken.None);
        Assert.Same(payment, storedPayment);
    }

    [Fact]
    public async Task StartAsync_StoresNewPayment()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();

        var result = await repository.StartAsync(payment, CancellationToken.None);

        Assert.Equal(PaymentStartStatus.Created, result.Status);
        Assert.Same(payment, result.Payment);
    }

    [Fact]
    public async Task StartAsync_ReturnsExistingPayment_WhenIdempotencyKeyMatchesSameRequest()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);

        var replay = CreatePendingPayment();
        var result = await repository.StartAsync(replay, CancellationToken.None);

        Assert.Equal(PaymentStartStatus.Existing, result.Status);
        Assert.Same(payment, result.Payment);
    }

    [Fact]
    public async Task StartAsync_ReturnsConflict_WhenIdempotencyKeyMatchesDifferentRequest()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);

        var conflictingPayment = Payment.CreatePending(
            Guid.NewGuid(),
            "merchant-001",
            "invoice-001",
            "different-fingerprint",
            "2222405343248877",
            12,
            2030,
            "GBP",
            100);

        var result = await repository.StartAsync(conflictingPayment, CancellationToken.None);

        Assert.Equal(PaymentStartStatus.Conflict, result.Status);
        Assert.Same(payment, result.Payment);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenPaymentDoesNotExist()
    {
        var repository = new PaymentsRepository();

        var payment = await repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(payment);
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
