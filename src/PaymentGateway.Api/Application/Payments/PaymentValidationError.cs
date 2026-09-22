namespace PaymentGateway.Api.Application.Payments;

public sealed record PaymentValidationError(string Code, string Message);
