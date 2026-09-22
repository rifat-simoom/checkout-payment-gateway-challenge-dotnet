using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentGateway.Api.Api.Contracts.Requests;
using PaymentGateway.Api.Api.Contracts.Responses;
using PaymentGateway.Api.Api.Controllers;
using PaymentGateway.Api.Application.Payments.Exceptions;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task PostPayment_ReturnsCreatedAuthorizedPayment()
    {
        var paymentId = Guid.NewGuid();
        var client = CreateClient(new FakePaymentsService
        {
            ProcessResult = ProcessPaymentResult.Authorized(
                new Payment(paymentId, PaymentStatus.Authorized, "8877", 12, 2030, "GBP", 100))
        });

        var response = await client.PostAsJsonAsync("/payments", ValidRequest());
        var responseBody = await response.Content.ReadAsStringAsync();
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/payments/{paymentId}", response.Headers.Location?.ToString());
        if (paymentResponse is null)
        {
            throw new InvalidOperationException("Payment response body was not returned.");
        }

        Assert.Equal(paymentId, paymentResponse.Id);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal("8877", paymentResponse.LastFourCardDigits);
        Assert.Equal(12, paymentResponse.ExpiryMonth);
        Assert.Equal(2030, paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(100, paymentResponse.Amount);
        AssertDoesNotExposeSensitiveCardData(responseBody);
    }

    [Fact]
    public async Task PostPayment_ReturnsCreatedDeclinedPayment()
    {
        var client = CreateClient(new FakePaymentsService
        {
            ProcessResult = ProcessPaymentResult.Declined(
                new Payment(Guid.NewGuid(), PaymentStatus.Declined, "8878", 12, 2030, "GBP", 100))
        });

        var response = await client.PostAsJsonAsync("/payments", ValidRequest());
        var responseBody = await response.Content.ReadAsStringAsync();
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        if (paymentResponse is null)
        {
            throw new InvalidOperationException("Payment response body was not returned.");
        }

        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
        Assert.Equal("8878", paymentResponse.LastFourCardDigits);
        AssertDoesNotExposeSensitiveCardData(responseBody);
    }

    [Fact]
    public async Task PostPayment_ReturnsBadRequestRejectedPayment()
    {
        var client = CreateClient(new FakePaymentsService
        {
            ProcessResult = ProcessPaymentResult.Rejected(new[]
            {
                new PaymentValidationError("InvalidCardNumber", "Card number is invalid.")
            })
        });

        var response = await client.PostAsJsonAsync("/payments", ValidRequest());
        var responseBody = await response.Content.ReadAsStringAsync();
        var rejectedResponse = await response.Content.ReadFromJsonAsync<PostPaymentRejectedResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        if (rejectedResponse is null)
        {
            throw new InvalidOperationException("Rejected response body was not returned.");
        }

        Assert.Equal(PaymentStatus.Rejected, rejectedResponse.Status);
        Assert.Contains(rejectedResponse.Errors, error => error.Code == "InvalidCardNumber");
        AssertDoesNotExposeSensitiveCardData(responseBody);
    }

    [Fact]
    public async Task PostPayment_ReturnsBadGateway_WhenBankIsUnavailable()
    {
        var client = CreateClient(new FakePaymentsService
        {
            ThrowBankUnavailable = true
        });

        var response = await client.PostAsJsonAsync("/payments", ValidRequest());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_ReturnsPayment()
    {
        var paymentId = Guid.NewGuid();
        var client = CreateClient(new FakePaymentsService
        {
            GetResult = new GetPaymentResult(paymentId, PaymentStatus.Authorized, "8877", 12, 2030, "GBP", 100)
        });

        var response = await client.GetAsync($"/payments/{paymentId}");
        var responseBody = await response.Content.ReadAsStringAsync();
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (paymentResponse is null)
        {
            throw new InvalidOperationException("Payment response body was not returned.");
        }

        Assert.Equal(paymentId, paymentResponse.Id);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal("8877", paymentResponse.LastFourCardDigits);
        AssertDoesNotExposeSensitiveCardData(responseBody);
    }

    [Fact]
    public async Task GetPayment_ReturnsNotFound_WhenPaymentDoesNotExist()
    {
        var client = CreateClient(new FakePaymentsService());

        var response = await client.GetAsync($"/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateClient(FakePaymentsService paymentsService)
    {
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();

        return webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPaymentsService>();
                services.AddSingleton<IPaymentsService>(paymentsService);
            }))
            .CreateClient();
    }

    private static PostPaymentRequest ValidRequest() =>
        new()
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

    private static void AssertDoesNotExposeSensitiveCardData(string responseBody)
    {
        Assert.DoesNotContain("\"cardNumber\"", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"cvv\"", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2222405343248877", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakePaymentsService : IPaymentsService
    {
        public ProcessPaymentResult ProcessResult { get; init; } =
            ProcessPaymentResult.Authorized(
                new Payment(Guid.NewGuid(), PaymentStatus.Authorized, "8877", 12, 2030, "GBP", 100));

        public GetPaymentResult? GetResult { get; init; }

        public bool ThrowBankUnavailable { get; init; }

        public Task<ProcessPaymentResult> ProcessAsync(
            ProcessPaymentCommand command,
            CancellationToken cancellationToken)
        {
            if (ThrowBankUnavailable)
            {
                throw new AcquiringBankUnavailableException("Bank unavailable.");
            }

            return Task.FromResult(ProcessResult);
        }

        public Task<GetPaymentResult?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetResult);
        }
    }
}
