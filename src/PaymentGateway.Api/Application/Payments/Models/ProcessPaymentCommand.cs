namespace PaymentGateway.Api.Application.Payments.Models;

public sealed record ProcessPaymentCommand(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv);
