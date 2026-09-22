using Microsoft.Extensions.Logging.Abstractions;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests.Application.Payments;

public sealed class PaymentsServiceTests
{
    private static readonly ProcessPaymentCommand AuthorizedCommand = new(
        "2222405343248877",
        12,
        2030,
        "GBP",
        100,
        "123");

    private static readonly ProcessPaymentCommand DeclinedCommand = new(
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
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Authorized, "8877", 12, 2030, "GBP", 100);
        var repository = new FakePaymentsRepository();
        await repository.AddAsync(payment, CancellationToken.None);
        var service = CreateService(new LastDigitBankClient(), repository);

        var result = await service.GetAsync(payment.Id, CancellationToken.None);

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

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

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

        public Task<AcquiringBankPaymentResult> ProcessAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;

            return Task.FromResult(new AcquiringBankPaymentResult(true));
        }
    }

    private sealed class FakePaymentsRepository : IPaymentsRepository
    {
        private readonly Dictionary<Guid, Payment> _payments = new();

        public IReadOnlyCollection<Payment> Payments => _payments.Values.ToArray();

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            _payments[payment.Id] = payment;

            return Task.CompletedTask;
        }

        public Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            _payments.TryGetValue(paymentId, out var payment);

            return Task.FromResult(payment);
        }
    }
}
