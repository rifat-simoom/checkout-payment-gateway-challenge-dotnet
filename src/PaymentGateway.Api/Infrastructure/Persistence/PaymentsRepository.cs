using System.Collections.Concurrent;
using PaymentGateway.Api.Api.Contracts.Responses;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();
    private readonly ConcurrentDictionary<string, Guid> _idempotencyKeys = new();
    private readonly object _syncRoot = new();
    
    public void Add(PostPaymentResponse payment)
    {
        _payments[payment.Id] = new Payment(
            payment.Id,
            payment.Status,
            string.Empty,
            string.Empty,
            string.Empty,
            payment.LastFourCardDigits,
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
                LastFourCardDigits = payment.LastFourCardDigits,
                ExpiryMonth = payment.ExpiryMonth,
                ExpiryYear = payment.ExpiryYear,
                Currency = payment.Currency,
                Amount = payment.Amount
            }
            : null;
    }

    public Task<PaymentStartResult> StartAsync(Payment payment, CancellationToken cancellationToken)
    {
        var idempotencyStorageKey = GetIdempotencyStorageKey(payment.MerchantId, payment.IdempotencyKey);

        lock (_syncRoot)
        {
            if (_idempotencyKeys.TryGetValue(idempotencyStorageKey, out var existingPaymentId) &&
                _payments.TryGetValue(existingPaymentId, out var existingPayment))
            {
                return Task.FromResult(
                    existingPayment.RequestFingerprint == payment.RequestFingerprint
                        ? PaymentStartResult.Existing(existingPayment)
                        : PaymentStartResult.Conflict(existingPayment));
            }

            _payments[payment.Id] = payment;
            _idempotencyKeys[idempotencyStorageKey] = payment.Id;

            return Task.FromResult(PaymentStartResult.Created(payment));
        }
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        _payments[payment.Id] = payment;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken)
    {
        _payments[payment.Id] = payment;

        return Task.CompletedTask;
    }

    public Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        _payments.TryGetValue(paymentId, out var payment);

        return Task.FromResult(payment);
    }

    private static string GetIdempotencyStorageKey(string merchantId, string idempotencyKey) =>
        $"{merchantId.Trim().ToUpperInvariant()}:{idempotencyKey.Trim().ToUpperInvariant()}";
}
