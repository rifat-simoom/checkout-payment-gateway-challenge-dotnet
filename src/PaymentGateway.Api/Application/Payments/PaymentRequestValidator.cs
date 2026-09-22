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

        AddCardNumberError(command, errors);
        AddExpiryMonthError(command, errors);
        AddExpiredCardError(command, errors);
        AddCurrencyError(command, errors);
        AddAmountError(command, errors);
        AddCvvError(command, errors);

        return errors;
    }

    private static void AddCardNumberError(
        ProcessPaymentCommand command,
        List<PaymentValidationError> errors)
    {
        if (!IsValidCardNumber(command.CardNumber))
        {
            errors.Add(new PaymentValidationError(
                "InvalidCardNumber",
                "Card number must contain 14 to 19 numeric characters."));
        }
    }

    private static void AddExpiryMonthError(
        ProcessPaymentCommand command,
        List<PaymentValidationError> errors)
    {
        if (!IsValidExpiryMonth(command.ExpiryMonth))
        {
            errors.Add(new PaymentValidationError(
                "InvalidExpiryMonth",
                "Expiry month must be between 1 and 12."));
        }
    }

    private static void AddExpiredCardError(
        ProcessPaymentCommand command,
        List<PaymentValidationError> errors)
    {
        if (IsExpired(command.ExpiryMonth, command.ExpiryYear))
        {
            errors.Add(new PaymentValidationError(
                "ExpiredCard",
                "Expiry month and year must not be in the past."));
        }
    }

    private static void AddCurrencyError(
        ProcessPaymentCommand command,
        List<PaymentValidationError> errors)
    {
        if (!IsValidCurrency(command.Currency))
        {
            errors.Add(new PaymentValidationError(
                "InvalidCurrency",
                "Currency must be one of GBP, USD, or EUR."));
        }
    }

    private static void AddAmountError(
        ProcessPaymentCommand command,
        List<PaymentValidationError> errors)
    {
        if (command.Amount <= 0)
        {
            errors.Add(new PaymentValidationError(
                "InvalidAmount",
                "Amount must be greater than zero."));
        }
    }

    private static void AddCvvError(
        ProcessPaymentCommand command,
        List<PaymentValidationError> errors)
    {
        if (!IsValidCvv(command.Cvv))
        {
            errors.Add(new PaymentValidationError(
                "InvalidCvv",
                "CVV must contain 3 to 4 numeric characters."));
        }
    }

    private static bool IsValidCardNumber(string cardNumber) =>
        !string.IsNullOrWhiteSpace(cardNumber) &&
        cardNumber.Length is >= 14 and <= 19 &&
        cardNumber.All(char.IsDigit);

    private static bool IsValidExpiryMonth(int expiryMonth) => expiryMonth is >= 1 and <= 12;

    private static bool IsValidCurrency(string currency) =>
        !string.IsNullOrWhiteSpace(currency) &&
        currency.Length == 3 &&
        SupportedCurrencies.Contains(currency);

    private static bool IsValidCvv(string cvv) =>
        !string.IsNullOrWhiteSpace(cvv) &&
        cvv.Length is >= 3 and <= 4 &&
        cvv.All(char.IsDigit);

    private static bool IsExpired(int expiryMonth, int expiryYear)
    {
        if (!IsValidExpiryMonth(expiryMonth))
        {
            return false;
        }

        var currentMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var expiry = new DateOnly(expiryYear, expiryMonth, 1);

        return expiry < currentMonth;
    }
}
