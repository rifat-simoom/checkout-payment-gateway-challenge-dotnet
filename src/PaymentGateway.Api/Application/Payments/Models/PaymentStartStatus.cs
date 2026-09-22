namespace PaymentGateway.Api.Application.Payments.Models;

public enum PaymentStartStatus
{
    Created,
    Retry,
    Existing,
    Conflict
}
