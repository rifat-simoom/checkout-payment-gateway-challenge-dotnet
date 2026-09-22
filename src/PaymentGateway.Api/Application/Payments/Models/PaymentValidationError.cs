namespace PaymentGateway.Api.Application.Payments.Models;

public sealed record PaymentValidationError(string Code, string Message);
