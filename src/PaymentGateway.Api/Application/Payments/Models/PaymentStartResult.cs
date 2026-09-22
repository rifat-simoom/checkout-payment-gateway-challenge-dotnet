using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments.Models;

public sealed record PaymentStartResult(PaymentStartStatus Status, Payment Payment)
{
    public static PaymentStartResult Created(Payment payment) =>
        new(PaymentStartStatus.Created, payment);

    public static PaymentStartResult Existing(Payment payment) =>
        new(PaymentStartStatus.Existing, payment);

    public static PaymentStartResult Conflict(Payment payment) =>
        new(PaymentStartStatus.Conflict, payment);
}
