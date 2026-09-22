namespace PaymentGateway.Api.Application.Payments.Exceptions;

public sealed class AcquiringBankOutcomeUnknownException : Exception
{
    public AcquiringBankOutcomeUnknownException(string message)
        : base(message)
    {
    }

    public AcquiringBankOutcomeUnknownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}