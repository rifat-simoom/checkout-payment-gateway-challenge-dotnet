namespace PaymentGateway.Api.Application.Payments;

public interface IAcquiringBankClient
{
    Task<AcquiringBankPaymentResult> ProcessAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken);
}
