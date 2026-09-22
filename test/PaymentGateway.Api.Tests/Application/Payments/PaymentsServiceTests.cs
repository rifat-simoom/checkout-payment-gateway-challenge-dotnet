using Microsoft.Extensions.Logging.Abstractions;
using PaymentGateway.Api.Application.Payments.Exceptions;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Application.Payments.Services;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests.Application.Payments;

public sealed class PaymentsServiceTests
{
    private static readonly ProcessPaymentCommand AuthorizedCommand = new(
        "merchant-001",
        "invoice-001",
        "2222405343248877",
        12,
        2030,
        "GBP",
        100,
        "123");

    private static readonly ProcessPaymentCommand DeclinedCommand = new(
        "merchant-001",
        "invoice-002",
        "2222405343248878",
        12,
        2030,
        "GBP",
        100,
        "123");

    [Fact]
    public async Task ProcessAsync_ReturnsAuthorizedPayment_WhenBankAuthorizesPayment()
    {
        var repository = new FakePaymentsRepository();
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.NotNull(result.Id);
        Assert.Equal("8877", result.LastFourCardDigits);
        Assert.Equal(AuthorizedCommand.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(AuthorizedCommand.ExpiryYear, result.ExpiryYear);
        Assert.Equal(AuthorizedCommand.Currency, result.Currency);
        Assert.Equal(AuthorizedCommand.Amount, result.Amount);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsDeclinedPayment_WhenBankDeclinesPayment()
    {
        var repository = new FakePaymentsRepository();
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.ProcessAsync(DeclinedCommand, CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, result.Status);
        Assert.NotNull(result.Id);
        Assert.Equal("8878", result.LastFourCardDigits);
    }

    [Fact]
    public async Task ProcessAsync_StoresAuthorizedPayment()
    {
        var repository = new FakePaymentsRepository();
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        if (result.Id is null)
        {
            throw new InvalidOperationException("Processed payment result did not include an id.");
        }

        var storedPayment = await repository.GetAsync(result.Id.Value, CancellationToken.None);
        if (storedPayment is null)
        {
            throw new InvalidOperationException("Processed payment was not stored.");
        }

        Assert.Equal(PaymentStatus.Authorized, storedPayment.Status);
    }

    [Fact]
    public async Task ProcessAsync_StoresPendingPaymentBeforeCallingBank()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new InspectingBankClient(repository);
        var service = CreateService(bankClient, repository);

        await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        Assert.Equal(PaymentStatus.Pending, bankClient.PaymentStatusDuringBankCall);
    }

    [Fact]
    public async Task ProcessAsync_StoresDeclinedPayment()
    {
        var repository = new FakePaymentsRepository();
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.ProcessAsync(DeclinedCommand, CancellationToken.None);

        if (result.Id is null)
        {
            throw new InvalidOperationException("Processed payment result did not include an id.");
        }

        var storedPayment = await repository.GetAsync(result.Id.Value, CancellationToken.None);
        if (storedPayment is null)
        {
            throw new InvalidOperationException("Processed payment was not stored.");
        }

        Assert.Equal(PaymentStatus.Declined, storedPayment.Status);
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredPayment()
    {
        var payment = CreateCompletedPayment("merchant-001");
        var repository = new FakePaymentsRepository();
        await repository.AddAsync(payment, CancellationToken.None);
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.GetAsync(payment.Id, "merchant-001", CancellationToken.None);

        if (result is null)
        {
            throw new InvalidOperationException("Stored payment was not returned.");
        }

        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(payment.Status, result.Status);
        Assert.Equal(payment.LastFourCardDigits, result.LastFourCardDigits);
        Assert.Equal(payment.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, result.ExpiryYear);
        Assert.Equal(payment.Currency, result.Currency);
        Assert.Equal(payment.Amount, result.Amount);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenPaymentDoesNotExist()
    {
        var service = CreateService(new LastDigitBankClient(), new FakePaymentsRepository());

        var result = await service.GetAsync(Guid.NewGuid(), "merchant-001", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenPaymentBelongsToDifferentMerchant()
    {
        var payment = CreateCompletedPayment("merchant-001");
        var repository = new FakePaymentsRepository();
        await repository.AddAsync(payment, CancellationToken.None);
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.GetAsync(payment.Id, "merchant-002", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsRejected_WhenCommandIsInvalid()
    {
        var service = CreateService(
            new TrackingBankClient(),
            new FakePaymentsRepository());

        var result = await service.ProcessAsync(
            AuthorizedCommand with { CardNumber = "invalid" },
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Rejected, result.Status);
        Assert.Null(result.Id);
        Assert.Contains(result.Errors, error => error.Code == "InvalidCardNumber");
    }

    [Fact]
    public async Task ProcessAsync_DoesNotCallBank_WhenCommandIsInvalid()
    {
        var bankClient = new TrackingBankClient();
        var service = CreateService(bankClient, new FakePaymentsRepository());

        await service.ProcessAsync(AuthorizedCommand with { CardNumber = "invalid" }, CancellationToken.None);

        Assert.False(bankClient.WasCalled);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotStorePayment_WhenCommandIsInvalid()
    {
        var repository = new FakePaymentsRepository();
        var service = CreateService(new TrackingBankClient(), repository);

        await service.ProcessAsync(AuthorizedCommand with { CardNumber = "invalid" }, CancellationToken.None);

        Assert.Empty(repository.Payments);
    }

    [Fact]
    public async Task ProcessAsync_LeavesPendingPayment_WhenBankIsUnavailable()
    {
        var repository = new FakePaymentsRepository();
        var service = CreateService(new UnavailableBankClient(), repository);

        await Assert.ThrowsAsync<AcquiringBankUnavailableException>(
            () => service.ProcessAsync(AuthorizedCommand, CancellationToken.None));

        var payment = Assert.Single(repository.Payments);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(payment.IsProcessing);
    }

    [Fact]
    public async Task ProcessAsync_RetriesBank_WhenPreviousAttemptFailed()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new FailsOnceBankClient();
        var service = CreateService(bankClient, repository);

        await Assert.ThrowsAsync<AcquiringBankUnavailableException>(
            () => service.ProcessAsync(AuthorizedCommand, CancellationToken.None));

        var result = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.False(result.IsNewPayment);
        Assert.Equal(2, bankClient.CallCount);
        Assert.Single(repository.Payments);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotRetryBank_WhenCompletionFailsAfterBankAuthorizes()
    {
        var repository = new FailingCompletePaymentsRepository();
        var bankClient = new TrackingBankClient();
        var service = CreateService(bankClient, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessAsync(AuthorizedCommand, CancellationToken.None));

        var replayResult = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        Assert.Equal(PaymentStatus.Pending, replayResult.Status);
        Assert.False(replayResult.IsNewPayment);
        Assert.Equal(1, bankClient.CallCount);

        var payment = Assert.Single(repository.Payments);
        Assert.True(payment.IsProcessing);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotRetryBank_WhenBankOutcomeIsUnknown()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new UnknownOutcomeBankClient();
        var service = CreateService(bankClient, repository);

        await Assert.ThrowsAsync<AcquiringBankOutcomeUnknownException>(
            () => service.ProcessAsync(AuthorizedCommand, CancellationToken.None));

        var replayResult = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        Assert.Equal(PaymentStatus.Pending, replayResult.Status);
        Assert.False(replayResult.IsNewPayment);
        Assert.Equal(1, bankClient.CallCount);

        var payment = Assert.Single(repository.Payments);
        Assert.True(payment.IsProcessing);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotConflict_WhenOnlyCvvChangesForSameIdempotencyKey()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new TrackingBankClient();
        var service = CreateService(bankClient, repository);

        var firstResult = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);
        var secondResult = await service.ProcessAsync(AuthorizedCommand with { Cvv = "999" }, CancellationToken.None);

        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.False(secondResult.IsNewPayment);
        Assert.Equal(1, bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsExistingPaymentAndDoesNotCallBank_WhenIdempotencyKeyIsReplayed()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new TrackingBankClient();
        var service = CreateService(bankClient, repository);

        var firstResult = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);
        var secondResult = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.False(secondResult.IsNewPayment);
        Assert.Equal(1, bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_AllowsSameIdempotencyKeyForDifferentMerchant()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new TrackingBankClient();
        var service = CreateService(bankClient, repository);

        var firstResult = await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);
        var secondResult = await service.ProcessAsync(
            AuthorizedCommand with { MerchantId = "merchant-002" },
            CancellationToken.None);

        Assert.NotEqual(firstResult.Id, secondResult.Id);
        Assert.Equal(2, bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ThrowsConflict_WhenIdempotencyKeyIsReusedForDifferentPayment()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new TrackingBankClient();
        var service = CreateService(bankClient, repository);

        await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);

        await Assert.ThrowsAsync<IdempotencyKeyConflictException>(
            () => service.ProcessAsync(AuthorizedCommand with { Amount = 200 }, CancellationToken.None));

        Assert.Equal(1, bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentlyReplaysSameIdempotencyKeyAndCallsBankOnce()
    {
        var repository = new FakePaymentsRepository();
        var bankClient = new ReleasableBankClient();
        var service = CreateService(bankClient, repository);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        const int requestCount = 8;

        var tasks = Enumerable.Range(0, requestCount)
            .Select(_ => Task.Run(async () =>
            {
                if (Interlocked.Increment(ref readyCount) == requestCount)
                {
                    ready.SetResult();
                }

                await start.Task;

                return await service.ProcessAsync(AuthorizedCommand, CancellationToken.None);
            }))
            .ToArray();

        await ready.Task;
        start.SetResult();
        await bankClient.WaitUntilCalledAsync();

        var pendingResults = await Task.WhenAll(tasks.Where(task => task.IsCompletedSuccessfully));
        Assert.All(pendingResults, result => Assert.Equal(PaymentStatus.Pending, result.Status));

        bankClient.Release();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, bankClient.CallCount);
        Assert.Single(results, result => result.IsNewPayment);
        Assert.Single(results.Select(result => result.Id).Distinct());
        Assert.All(results, result => Assert.Contains(result.Status, new[] { PaymentStatus.Pending, PaymentStatus.Authorized }));
    }

    private sealed class LastDigitBankClient : IAcquiringBankClient
    {
        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            var lastDigit = request.CardNumber[^1] - '0';

            return Task.FromResult(new AcquiringBankPaymentResult(lastDigit % 2 == 1));
        }
    }

    private static PaymentsService CreateService(
        IAcquiringBankClient bankClient,
        IPaymentsRepository repository) =>
        new(bankClient, repository, NullLogger<PaymentsService>.Instance);

    private sealed class TrackingBankClient : IAcquiringBankClient
    {
        public bool WasCalled { get; private set; }

        public int CallCount { get; private set; }

        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            CallCount++;

            return Task.FromResult(new AcquiringBankPaymentResult(true));
        }
    }

    private sealed class ReleasableBankClient : IAcquiringBankClient
    {
        private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => _callCount;

        public async Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            _called.SetResult();

            await _release.Task.WaitAsync(cancellationToken);

            return new AcquiringBankPaymentResult(true);
        }

        public Task WaitUntilCalledAsync() => _called.Task;

        public void Release() => _release.SetResult();
    }

    private sealed class InspectingBankClient : IAcquiringBankClient
    {
        private readonly FakePaymentsRepository _repository;

        public InspectingBankClient(FakePaymentsRepository repository)
        {
            _repository = repository;
        }

        public PaymentStatus? PaymentStatusDuringBankCall { get; private set; }

        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            PaymentStatusDuringBankCall = Assert.Single(_repository.Payments).Status;

            return Task.FromResult(new AcquiringBankPaymentResult(true));
        }
    }

    private sealed class UnavailableBankClient : IAcquiringBankClient
    {
        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            throw new AcquiringBankUnavailableException("Bank unavailable.");
        }
    }

    private sealed class FailsOnceBankClient : IAcquiringBankClient
    {
        public int CallCount { get; private set; }

        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount == 1)
            {
                throw new AcquiringBankUnavailableException("Bank unavailable.");
            }

            return Task.FromResult(new AcquiringBankPaymentResult(true));
        }
    }

    private sealed class UnknownOutcomeBankClient : IAcquiringBankClient
    {
        public int CallCount { get; private set; }

        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            throw new AcquiringBankOutcomeUnknownException(
                "Bank outcome is unknown.",
                new TaskCanceledException("The request timed out."));
        }
    }

    private class FakePaymentsRepository : IPaymentsRepository
    {
        private readonly Dictionary<Guid, Payment> _payments = new();
        private readonly Dictionary<string, Guid> _idempotencyKeys = new();
        private readonly object _syncRoot = new();

        public IReadOnlyCollection<Payment> Payments
        {
            get
            {
                lock (_syncRoot)
                {
                    return _payments.Values.ToArray();
                }
            }
        }

        public Task<PaymentStartResult> StartAsync(Payment payment, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                var storageKey = $"{payment.MerchantId}:{payment.IdempotencyKey}";
                if (_idempotencyKeys.TryGetValue(storageKey, out var existingPaymentId))
                {
                    var existingPayment = _payments[existingPaymentId];
                    if (existingPayment.RequestFingerprint != payment.RequestFingerprint)
                    {
                        return Task.FromResult(PaymentStartResult.Conflict(existingPayment));
                    }

                    if (existingPayment.Status == PaymentStatus.Pending && !existingPayment.IsProcessing)
                    {
                        existingPayment.MarkProcessing();

                        return Task.FromResult(PaymentStartResult.Retry(existingPayment));
                    }

                    return Task.FromResult(PaymentStartResult.Existing(existingPayment));
                }

                _payments[payment.Id] = payment;
                _idempotencyKeys[storageKey] = payment.Id;

                return Task.FromResult(PaymentStartResult.Created(payment));
            }
        }

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                _payments[payment.Id] = payment;
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(Payment payment, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                _payments[payment.Id] = payment;
            }

            return Task.CompletedTask;
        }

        public virtual Task<Payment> CompleteAsync(Guid paymentId, bool authorized, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                var payment = _payments[paymentId];
                if (payment.Status == PaymentStatus.Pending)
                {
                    if (authorized)
                    {
                        payment.Authorize();
                    }
                    else
                    {
                        payment.Decline();
                    }
                }

                return Task.FromResult(payment);
            }
        }

        public Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                _payments.TryGetValue(paymentId, out var payment);

                return Task.FromResult(payment);
            }
        }

        public Task MarkProcessingFailedAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                if (_payments.TryGetValue(paymentId, out var payment))
                {
                    payment.MarkProcessingFailed();
                }

                return Task.CompletedTask;
            }
        }
    }

    private sealed class FailingCompletePaymentsRepository : FakePaymentsRepository
    {
        public override Task<Payment> CompleteAsync(Guid paymentId, bool authorized, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Completion failed.");
        }
    }

    private static Payment CreateCompletedPayment(string merchantId)
    {
        var payment = Payment.CreatePending(
            Guid.NewGuid(),
            merchantId,
            "invoice-001",
            "fingerprint-001",
            "2222405343248877",
            12,
            2030,
            "GBP",
            100);
        payment.Authorize();

        return payment;
    }
}
