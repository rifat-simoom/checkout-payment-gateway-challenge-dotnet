# Payment Gateway Challenge

[![CI](https://github.com/rifat-simoom/checkout-payment-gateway-challenge-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/rifat-simoom/checkout-payment-gateway-challenge-dotnet/actions/workflows/ci.yml)

This is a .NET payment gateway implementation for the Checkout.com technical challenge. It exposes a small API that validates payment requests, forwards valid payments to the bank simulator, stores attempted payments in memory, and returns only safe card details.

## Architecture

The implementation keeps the solution deliberately small while separating responsibilities:

- `Api` contains HTTP controllers and request/response contracts.
- `Application` contains payment use cases, validation, ports, and result types.
- `Domain` contains payment concepts used by the application.
- `Infrastructure` contains the acquiring bank HTTP client and in-memory persistence.

The gateway stores payments in memory, so data is reset when the API process restarts. This keeps the challenge focused on API behavior, validation, bank integration, tests, and safe handling of payment data.

## API

The payment gateway exposes:

```text
POST /payments
GET /payments/{id}
```

`POST /payments` processes a payment and returns `201 Created` for bank-attempted payments with status `Authorized` or `Declined`. Invalid gateway requests return `400 Bad Request` with status `Rejected`. Bank simulator failures return `502 Bad Gateway`.

`GET /payments/{id}` returns a stored payment or `404 Not Found`.

Responses return safe card details only: the full card number and CVV are not returned.

Example request:

```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

## Running Locally

Restore, build, and test the solution:

```bash
dotnet restore PaymentGateway.sln
dotnet build PaymentGateway.sln --configuration Release --no-restore
dotnet test PaymentGateway.sln --configuration Release --no-build
```

Start the bank simulator:

```bash
docker-compose up
```

Run the API:

```bash
dotnet run --project src/PaymentGateway.Api
```

The HTTP launch profile uses `http://localhost:5067`. Swagger is available in development at `/swagger`.

The bank simulator URL is configured with `BankSimulator:BaseUrl` in `appsettings.json` and defaults to `http://localhost:8080`.

## Postman

A Postman collection is available at:

```text
postman/PaymentGateway.postman_collection.json
```

The collection uses `http://localhost:5067` by default, matching the HTTP launch profile.

## Observability

The API uses structured application logs for payment processing, validation rejection, payment retrieval, and bank simulator calls. Logs intentionally avoid full card numbers, CVV values, and raw payment payloads.

Safe log fields include payment id, status, currency, amount, validation error codes, bank status code, and elapsed milliseconds.

## Tests

The test suite covers:

- Application payment behavior for authorized, declined, rejected, stored, and retrieved payments.
- Payment validation rules.
- Acquiring bank request/response mapping and unavailable-bank behavior.
- API endpoint status codes and safe response bodies.
- In-memory repository behavior.

GitHub Actions runs restore, release build, and tests on pushes and pull requests.

## Assumptions And Tradeoffs

- Supported currencies are `GBP`, `USD`, and `EUR`.
- Expiry validation accepts cards expiring in the current month.
- Bank simulator failures are returned as `502 Bad Gateway`.
- Persistence is in memory for challenge simplicity.
- The API avoids MediatR, CQRS, Kubernetes, and broader production infrastructure because they are unnecessary for this scope.

## Future Improvements

- Replace in-memory persistence with durable storage.
- Add correlation IDs and request tracing.
- Add authentication, authorization, and rate limiting.
- Add contract tests against the bank simulator.
- Add production-grade secret/configuration management.

## Template structure
```
src/
    PaymentGateway.Api - ASP.NET Core Web API
test/
    PaymentGateway.Api.Tests - xUnit test project
imposters/ - contains the bank simulator configuration. Don't change this

.editorconfig - don't change this. It ensures a consistent set of rules for submissions when reformatting code
docker-compose.yml - configures the bank simulator
PaymentGateway.sln
```
