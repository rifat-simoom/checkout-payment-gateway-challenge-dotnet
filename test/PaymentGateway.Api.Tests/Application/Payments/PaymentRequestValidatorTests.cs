using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Application.Payments.Validators;

namespace PaymentGateway.Api.Tests.Application.Payments;

public sealed class PaymentRequestValidatorTests
{
    private readonly PaymentRequestValidator _validator = new();

    public static IEnumerable<object[]> InvalidCommands()
    {
        yield return new object[] { ValidCommand() with { CardNumber = "" }, "InvalidCardNumber" };
        yield return new object[] { ValidCommand() with { CardNumber = "1234567890123" }, "InvalidCardNumber" };
        yield return new object[] { ValidCommand() with { CardNumber = "12345678901234567890" }, "InvalidCardNumber" };
        yield return new object[] { ValidCommand() with { CardNumber = "12345678901234A" }, "InvalidCardNumber" };
        yield return new object[] { ValidCommand() with { ExpiryMonth = 0 }, "InvalidExpiryMonth" };
        yield return new object[] { ValidCommand() with { ExpiryMonth = 13 }, "InvalidExpiryMonth" };
        yield return new object[] { ValidCommand() with { ExpiryMonth = 1, ExpiryYear = 2020 }, "ExpiredCard" };
        yield return new object[] { ValidCommand() with { Currency = "" }, "InvalidCurrency" };
        yield return new object[] { ValidCommand() with { Currency = "GB" }, "InvalidCurrency" };
        yield return new object[] { ValidCommand() with { Currency = "AUD" }, "InvalidCurrency" };
        yield return new object[] { ValidCommand() with { Amount = 0 }, "InvalidAmount" };
        yield return new object[] { ValidCommand() with { Amount = -1 }, "InvalidAmount" };
        yield return new object[] { ValidCommand() with { Cvv = "" }, "InvalidCvv" };
        yield return new object[] { ValidCommand() with { Cvv = "12" }, "InvalidCvv" };
        yield return new object[] { ValidCommand() with { Cvv = "12345" }, "InvalidCvv" };
        yield return new object[] { ValidCommand() with { Cvv = "12A" }, "InvalidCvv" };
    }

    public static IEnumerable<object[]> ValidCommands()
    {
        yield return new object[] { ValidCommand() with { CardNumber = "12345678901234" } };
        yield return new object[] { ValidCommand() with { CardNumber = "1234567890123456789" } };
        yield return new object[] { ValidCommand() with { ExpiryMonth = DateTime.UtcNow.Month, ExpiryYear = DateTime.UtcNow.Year } };
        yield return new object[] { ValidCommand() with { Currency = "GBP" } };
        yield return new object[] { ValidCommand() with { Currency = "USD" } };
        yield return new object[] { ValidCommand() with { Currency = "EUR" } };
        yield return new object[] { ValidCommand() with { Cvv = "123" } };
        yield return new object[] { ValidCommand() with { Cvv = "1234" } };
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public void Validate_ReturnsExpectedError_ForInvalidCommand(
        ProcessPaymentCommand command,
        string expectedErrorCode)
    {
        var errors = _validator.Validate(command);

        Assert.Contains(errors, error => error.Code == expectedErrorCode);
    }

    [Theory]
    [MemberData(nameof(ValidCommands))]
    public void Validate_ReturnsNoErrors_ForValidCommand(ProcessPaymentCommand command)
    {
        var errors = _validator.Validate(command);

        Assert.Empty(errors);
    }

    private static ProcessPaymentCommand ValidCommand() =>
        new(
            "2222405343248877",
            12,
            DateTime.UtcNow.Year + 1,
            "GBP",
            100,
            "123");
}
