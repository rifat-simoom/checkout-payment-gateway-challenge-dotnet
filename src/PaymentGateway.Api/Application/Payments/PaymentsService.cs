using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments;

public sealed class PaymentsService : IPaymentsService
{
    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsService(
        IAcquiringBankClient acquiringBankClient,
        IPaymentsRepository paymentsRepository)
    {
        _acquiringBankClient = acquiringBankClient;
        _paymentsRepository = paymentsRepository;
    }

    public async Task<ProcessPaymentResult> ProcessAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var bankResult = await _acquiringBankClient.ProcessAsync(
            new AcquiringBankPaymentRequest(
                command.CardNumber,
                command.ExpiryMonth,
                command.ExpiryYear,
                command.Currency,
                command.Amount,
                command.Cvv),
            cancellationToken);

        var payment = new Payment(
            Guid.NewGuid(),
            bankResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            command.CardNumber[^4..],
            command.ExpiryMonth,
            command.ExpiryYear,
            command.Currency,
            command.Amount);

        await _paymentsRepository.AddAsync(payment, cancellationToken);

        return payment.Status == PaymentStatus.Authorized
            ? ProcessPaymentResult.Authorized(payment)
            : ProcessPaymentResult.Declined(payment);
    }

    public async Task<GetPaymentResult?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _paymentsRepository.GetAsync(paymentId, cancellationToken);

        return payment is null ? null : GetPaymentResult.FromPayment(payment);
    }
}
