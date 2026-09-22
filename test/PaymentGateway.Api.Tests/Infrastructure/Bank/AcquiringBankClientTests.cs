using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Infrastructure.Bank;

namespace PaymentGateway.Api.Tests.Infrastructure.Bank;

public sealed class AcquiringBankClientTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsync_MapsBankAuthorizationResponse(bool authorized)
    {
        var client = CreateClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { authorized })
            }));

        var result = await client.ProcessAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(authorized, result.Authorized);
    }

    [Fact]
    public async Task ProcessAsync_PostsExpectedBankRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { authorized = true })
            };
        }));

        await client.ProcessAsync(ValidRequest(), CancellationToken.None);

        if (capturedRequest is null)
        {
            throw new InvalidOperationException("Bank request was not captured.");
        }

        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("http://bank.test/payments", capturedRequest.RequestUri?.ToString());

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("2222405343248877", root.GetProperty("card_number").GetString());
        Assert.Equal("12/2030", root.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", root.GetProperty("currency").GetString());
        Assert.Equal(100, root.GetProperty("amount").GetInt32());
        Assert.Equal("123", root.GetProperty("cvv").GetString());
    }

    [Fact]
    public async Task ProcessAsync_ThrowsUnavailableException_WhenBankReturnsFailure()
    {
        var client = CreateClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<AcquiringBankUnavailableException>(
            () => client.ProcessAsync(ValidRequest(), CancellationToken.None));
    }

    private static AcquiringBankClient CreateClient(HttpMessageHandler messageHandler)
    {
        var httpClient = new HttpClient(messageHandler);
        var options = Options.Create(new BankSimulatorOptions
        {
            BaseUrl = new Uri("http://bank.test")
        });

        return new AcquiringBankClient(httpClient, options);
    }

    private static AcquiringBankPaymentRequest ValidRequest() =>
        new("2222405343248877", 12, 2030, "GBP", 100, "123");

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_send(request));
        }
    }
}
