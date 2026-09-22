using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Api.Contracts.Responses;

public class GetPaymentResponse
{
    public Guid Id { get; set; }
    public PaymentStatus Status { get; set; }
    public string LastFourCardDigits { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int Amount { get; set; }
}
