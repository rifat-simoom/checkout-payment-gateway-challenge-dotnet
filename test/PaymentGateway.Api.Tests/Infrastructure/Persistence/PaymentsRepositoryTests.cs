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
    public async Task CompleteAsync_AuthorizesPendingPayment()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);

        var completedPayment = await repository.CompleteAsync(payment.Id, true, CancellationToken.None);

        Assert.Same(payment, completedPayment);
        Assert.Equal(PaymentStatus.Authorized, completedPayment.Status);
    }

    [Fact]
    public async Task CompleteAsync_DeclinesPendingPayment()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);

        var completedPayment = await repository.CompleteAsync(payment.Id, false, CancellationToken.None);

        Assert.Same(payment, completedPayment);
        Assert.Equal(PaymentStatus.Declined, completedPayment.Status);
    }

    [Fact]
    public async Task CompleteAsync_Throws_WhenPaymentIsAlreadyCompleted()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);
        await repository.CompleteAsync(payment.Id, true, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.CompleteAsync(payment.Id, false, CancellationToken.None));
    }

    [Fact]
    public async Task MarkProcessingFailedAsync_MakesPendingPaymentRetryable()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);

        await repository.MarkProcessingFailedAsync(payment.Id, CancellationToken.None);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(payment.IsProcessing);
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
    public async Task StartAsync_ReturnsRetry_WhenExistingPendingPaymentIsNotProcessing()
    {
        var repository = new PaymentsRepository();
        var payment = CreatePendingPayment();
        await repository.StartAsync(payment, CancellationToken.None);
        await repository.MarkProcessingFailedAsync(payment.Id, CancellationToken.None);

        var retryPayment = CreatePendingPayment();
        var result = await repository.StartAsync(retryPayment, CancellationToken.None);

        Assert.Equal(PaymentStartStatus.Retry, result.Status);
        Assert.Same(payment, result.Payment);
        Assert.True(result.Payment.IsProcessing);
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
    public async Task StartAsync_ConcurrentlyCreatesOnlyOnePaymentForSameIdempotencyKey()
    {
        var repository = new PaymentsRepository();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        const int requestCount = 12;

        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => Task.Run(async () =>
            {
                if (Interlocked.Increment(ref readyCount) == requestCount)
                {
                    ready.SetResult();
                }

                await start.Task;

                return await repository.StartAsync(CreatePendingPayment(), CancellationToken.None);
            }))
            .ToArray();

        await ready.Task;
        start.SetResult();
        var results = await Task.WhenAll(tasks);

        var createdResult = Assert.Single(results, result => result.Status == PaymentStartStatus.Created);
        Assert.Equal(requestCount - 1, results.Count(result => result.Status == PaymentStartStatus.Existing));
        Assert.All(results, result => Assert.Same(createdResult.Payment, result.Payment));
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
