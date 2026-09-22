namespace PaymentGateway.Api.Application.Payments;

public sealed class AcquiringBankUnavailableException : Exception
{
    public AcquiringBankUnavailableException(string message)
        : base(message)
    {
    }

    public AcquiringBankUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
