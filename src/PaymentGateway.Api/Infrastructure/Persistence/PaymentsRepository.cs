using System.Collections.Concurrent;
using PaymentGateway.Api.Api.Contracts.Responses;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();
    
    public void Add(PostPaymentResponse payment)
    {
        _payments[payment.Id] = new Payment(
            payment.Id,
            payment.Status,
            payment.CardNumberLastFour.ToString("0000"),
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount);
    }

    public PostPaymentResponse? Get(Guid id)
    {
        return _payments.TryGetValue(id, out var payment)
            ? new PostPaymentResponse
            {
                Id = payment.Id,
                Status = payment.Status,
                CardNumberLastFour = int.Parse(payment.LastFourCardDigits),
                ExpiryMonth = payment.ExpiryMonth,
                ExpiryYear = payment.ExpiryYear,
                Currency = payment.Currency,
                Amount = payment.Amount
            }
            : null;
    }

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
