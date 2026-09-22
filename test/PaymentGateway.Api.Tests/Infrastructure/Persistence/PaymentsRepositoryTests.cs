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
        var payment = Payment.CreatePending(Guid.NewGuid(), "2222405343248877", 12, 2030, "GBP", 100);
        await repository.AddAsync(payment, CancellationToken.None);

        payment.Authorize();
        await repository.UpdateAsync(payment, CancellationToken.None);

        var storedPayment = await repository.GetAsync(payment.Id, CancellationToken.None);
        Assert.Same(payment, storedPayment);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenPaymentDoesNotExist()
    {
        var repository = new PaymentsRepository();

        var payment = await repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(payment);
    }
}
