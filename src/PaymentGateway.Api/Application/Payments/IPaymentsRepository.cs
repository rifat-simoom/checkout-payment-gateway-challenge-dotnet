using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments;

public interface IPaymentsRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken);
}
