# Instructions for candidates

[![CI](https://github.com/rifat-simoom/checkout-payment-gateway-challenge-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/rifat-simoom/checkout-payment-gateway-challenge-dotnet/actions/workflows/ci.yml)

This is the .NET version of the Payment Gateway challenge. If you haven't already read this [README.md](https://github.com/cko-recruitment/) on the details of this exercise, please do so now. 

## API

The payment gateway exposes:

```text
POST /payments
GET /payments/{id}
```

`POST /payments` processes a payment and returns `201 Created` for bank-attempted payments with status `Authorized` or `Declined`. Invalid gateway requests return `400 Bad Request` with status `Rejected`. Bank simulator failures return `502 Bad Gateway`.

`GET /payments/{id}` returns a stored payment or `404 Not Found`.

Responses return safe card details only: the full card number and CVV are not returned.

## Postman

A Postman collection is available at:

```text
postman/PaymentGateway.postman_collection.json
```

The collection uses `http://localhost:5067` by default, matching the HTTP launch profile.

## Template structure
```
src/
    PaymentGateway.Api - a skeleton ASP.NET Core Web API
test/
    PaymentGateway.Api.Tests - an empty xUnit test project
imposters/ - contains the bank simulator configuration. Don't change this

.editorconfig - don't change this. It ensures a consistent set of rules for submissions when reformatting code
docker-compose.yml - configures the bank simulator
PaymentGateway.sln
```

Feel free to change the structure of the solution, use a different test library etc.
