using PaymentGateway.Api.Domain.Payments;
using PaymentGateway.Api.Application.Payments.Models;

namespace PaymentGateway.Api.Application.Payments.Interfaces;

public interface IPaymentsRepository
{
    Task<PaymentStartResult> StartAsync(Payment payment, CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task UpdateAsync(Payment payment, CancellationToken cancellationToken);

    Task<Payment> CompleteAsync(Guid paymentId, bool authorized, CancellationToken cancellationToken);

    Task MarkProcessingFailedAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken);
}
