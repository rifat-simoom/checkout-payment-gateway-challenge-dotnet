using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Api.Contracts.Responses;

public sealed class PostPaymentRejectedResponse
{
    public PaymentStatus Status { get; set; }

    public IReadOnlyCollection<PaymentValidationError> Errors { get; set; } =
        Array.Empty<PaymentValidationError>();
}