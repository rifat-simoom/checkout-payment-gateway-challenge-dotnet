namespace PaymentGateway.Api.Domain.Payments;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Declined,
    Rejected
}
