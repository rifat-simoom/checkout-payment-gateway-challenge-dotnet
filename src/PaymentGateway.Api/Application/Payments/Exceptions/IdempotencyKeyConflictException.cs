namespace PaymentGateway.Api.Application.Payments.Exceptions;

public sealed class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException()
        : base("Idempotency key has already been used for a different payment request.")
    {
    }
}