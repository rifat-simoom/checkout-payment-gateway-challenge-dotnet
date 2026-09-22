using PaymentGateway.Api.Application.Payments.Models;

namespace PaymentGateway.Api.Application.Payments;

public sealed class PaymentRequestValidator
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP",
        "USD",
        "EUR"
    };

    public IReadOnlyCollection<PaymentValidationError> Validate(ProcessPaymentCommand command)
    {
        var errors = new List<PaymentValidationError>();

        if (string.IsNullOrWhiteSpace(command.CardNumber) ||
            command.CardNumber.Length is < 14 or > 19 ||
            !command.CardNumber.All(char.IsDigit))
        {
            errors.Add(new PaymentValidationError(
                "InvalidCardNumber",
                "Card number must contain 14 to 19 numeric characters."));
        }

        if (command.ExpiryMonth is < 1 or > 12)
        {
            errors.Add(new PaymentValidationError(
                "InvalidExpiryMonth",
                "Expiry month must be between 1 and 12."));
        }

        if (IsExpired(command.ExpiryMonth, command.ExpiryYear))
        {
            errors.Add(new PaymentValidationError(
                "ExpiredCard",
                "Expiry month and year must not be in the past."));
        }

        if (string.IsNullOrWhiteSpace(command.Currency) ||
            command.Currency.Length != 3 ||
            !SupportedCurrencies.Contains(command.Currency))
        {
            errors.Add(new PaymentValidationError(
                "InvalidCurrency",
                "Currency must be one of GBP, USD, or EUR."));
        }

        if (command.Amount <= 0)
        {
            errors.Add(new PaymentValidationError(
                "InvalidAmount",
                "Amount must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(command.Cvv) ||
            command.Cvv.Length is < 3 or > 4 ||
            !command.Cvv.All(char.IsDigit))
        {
            errors.Add(new PaymentValidationError(
                "InvalidCvv",
                "CVV must contain 3 to 4 numeric characters."));
        }

        return errors;
    }

    private static bool IsExpired(int expiryMonth, int expiryYear)
    {
        if (expiryMonth is < 1 or > 12)
        {
            return false;
        }

        var currentMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var expiry = new DateOnly(expiryYear, expiryMonth, 1);

        return expiry < currentMonth;
    }
}
