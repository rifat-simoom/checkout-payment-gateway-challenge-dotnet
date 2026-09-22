using PaymentGateway.Api.Application.Payments.Models;

namespace PaymentGateway.Api.Application.Payments.Interfaces;

public interface IAcquiringBankClient
{
    Task<AcquiringBankPaymentResult> ProcessAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken);
}
