using System.Collections.Concurrent;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();
    private readonly ConcurrentDictionary<string, Guid> _idempotencyKeys = new();
    private readonly object _syncRoot = new();
    public Task<PaymentStartResult> StartAsync(Payment payment, CancellationToken cancellationToken)
    {
        var idempotencyStorageKey = GetIdempotencyStorageKey(payment.MerchantId, payment.IdempotencyKey);

        lock (_syncRoot)
        {
            if (_idempotencyKeys.TryGetValue(idempotencyStorageKey, out var existingPaymentId) &&
                _payments.TryGetValue(existingPaymentId, out var existingPayment))
            {
                if (existingPayment.RequestFingerprint != payment.RequestFingerprint)
                {
                    return Task.FromResult(PaymentStartResult.Conflict(existingPayment));
                }

                if (existingPayment.Status == PaymentStatus.Pending && !existingPayment.IsProcessing)
                {
                    existingPayment.MarkProcessing();

                    return Task.FromResult(PaymentStartResult.Retry(existingPayment));
                }

                return Task.FromResult(
                    PaymentStartResult.Existing(existingPayment));
            }

            _payments[payment.Id] = payment;
            _idempotencyKeys[idempotencyStorageKey] = payment.Id;

            return Task.FromResult(PaymentStartResult.Created(payment));
        }
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _payments[payment.Id] = payment;
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _payments[payment.Id] = payment;
        }

        return Task.CompletedTask;
    }

    public Task<Payment> CompleteAsync(Guid paymentId, bool authorized, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!_payments.TryGetValue(paymentId, out var payment))
            {
                throw new InvalidOperationException("Payment could not be completed because it was not found.");
            }

            if (payment.Status == PaymentStatus.Pending)
            {
                if (authorized)
                {
                    payment.Authorize();
                }
                else
                {
                    payment.Decline();
                }
            }

            return Task.FromResult(payment);
        }
    }

    public Task MarkProcessingFailedAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (_payments.TryGetValue(paymentId, out var payment))
            {
                payment.MarkProcessingFailed();
            }

            return Task.CompletedTask;
        }
    }

    public Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _payments.TryGetValue(paymentId, out var payment);

            return Task.FromResult(payment);
        }
    }

    private static string GetIdempotencyStorageKey(string merchantId, string idempotencyKey) =>
        $"{merchantId.Trim().ToUpperInvariant()}:{idempotencyKey.Trim().ToUpperInvariant()}";
}
