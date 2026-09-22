using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.Application.Payments.Exceptions;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;

namespace PaymentGateway.Api.Infrastructure.Bank;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(
        HttpClient httpClient,
        IOptions<BankSimulatorOptions> options,
        ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = options.Value.BaseUrl;
    }

    public async Task<AcquiringBankPaymentResult> ProcessAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/payments",
                BankPaymentRequest.FromPaymentRequest(request),
                cancellationToken);

            _logger.LogInformation(
                "Acquiring bank responded with {BankStatusCode} in {ElapsedMilliseconds}ms.",
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Acquiring bank returned non-success status {BankStatusCode} in {ElapsedMilliseconds}ms.",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

                throw new AcquiringBankUnavailableException(
                    $"Acquiring bank returned {(int)response.StatusCode}.");
            }

            var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(
                cancellationToken: cancellationToken);

            if (bankResponse is null)
            {
                _logger.LogWarning(
                    "Acquiring bank returned an empty response in {ElapsedMilliseconds}ms.",
                    stopwatch.ElapsedMilliseconds);

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
            _logger.LogError(
                exception,
                "Acquiring bank request failed after {ElapsedMilliseconds}ms.",
                stopwatch.ElapsedMilliseconds);

            throw new AcquiringBankUnavailableException("Acquiring bank request failed.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Acquiring bank request timed out after {ElapsedMilliseconds}ms.",
                stopwatch.ElapsedMilliseconds);

            throw new AcquiringBankUnavailableException("Acquiring bank request timed out.", exception);
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
