using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Infrastructure.Bank;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private readonly HttpClient _httpClient;

    public AcquiringBankClient(HttpClient httpClient, IOptions<BankSimulatorOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = options.Value.BaseUrl;
    }

    public async Task<AcquiringBankPaymentResult> ProcessAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/payments",
                BankPaymentRequest.FromPaymentRequest(request),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AcquiringBankUnavailableException(
                    $"Acquiring bank returned {(int)response.StatusCode}.");
            }

            var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(
                cancellationToken: cancellationToken);

            if (bankResponse is null)
            {
                throw new AcquiringBankUnavailableException("Acquiring bank returned an empty response.");
            }

            return new AcquiringBankPaymentResult(bankResponse.Authorized);
        }
        catch (AcquiringBankUnavailableException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new AcquiringBankUnavailableException("Acquiring bank request failed.", exception);
        }
    }

    private sealed record BankPaymentRequest(
        [property: JsonPropertyName("card_number")] string CardNumber,
        [property: JsonPropertyName("expiry_date")] string ExpiryDate,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("cvv")] string Cvv)
    {
        public static BankPaymentRequest FromPaymentRequest(AcquiringBankPaymentRequest request) =>
            new(
                request.CardNumber,
                $"{request.ExpiryMonth:00}/{request.ExpiryYear}",
                request.Currency,
                request.Amount,
                request.Cvv);
    }

    private sealed record BankPaymentResponse(
        [property: JsonPropertyName("authorized")] bool Authorized);
}
