namespace PaymentGateway.Api.Application.Payments;

public interface IPaymentsService
{
    Task<ProcessPaymentResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken cancellationToken);

    Task<GetPaymentResult?> GetAsync(Guid paymentId, CancellationToken cancellationToken);
}
