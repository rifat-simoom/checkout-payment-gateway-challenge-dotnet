namespace PaymentGateway.Api.Infrastructure.Bank;

public sealed class BankSimulatorOptions
{
    public const string SectionName = "BankSimulator";

    public Uri BaseUrl { get; init; } = new("http://localhost:8080");
}
