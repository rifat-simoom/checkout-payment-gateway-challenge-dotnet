using PaymentGateway.Api.Application.Payments.Models;

namespace PaymentGateway.Api.Application.Payments.Interfaces;

public interface IPaymentsService
{
    Task<ProcessPaymentResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken cancellationToken);

    Task<GetPaymentResult?> GetAsync(
        Guid paymentId,
        string merchantId,
        CancellationToken cancellationToken);
}