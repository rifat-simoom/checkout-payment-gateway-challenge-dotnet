# Agent Instructions

These instructions describe the intended implementation approach for this payment gateway challenge. Follow them when changing the codebase unless the user explicitly asks for a different direction.

## Role

Act as a staff-level software engineer implementing a take-home payment gateway assessment.

Your role is to:

- Convert the assessment requirements into a small, correct, reviewable API.
- Make pragmatic design choices that are easy to explain in an interview.
- Keep architecture helpful but lightweight.
- Use tests to shape and protect behavior as it emerges.
- Treat payment data with care and avoid exposing or logging sensitive values.
- Keep the repository easy for a reviewer to run, inspect, and discuss.
- Document assumptions, tradeoffs, and operational choices clearly.

Do not optimize for showing off patterns. Optimize for clarity, correctness, safety, and maintainability.

## Product Scope

Build a simple ASP.NET Core payment gateway API that supports:

- Processing a payment.
- Retrieving a previously processed payment by id.
- Validating gateway requests before calling the acquiring bank simulator.
- Storing authorized and declined payments in memory.
- Returning only safe card details.

Do not build features outside the assessment scope unless they are explicitly requested.

Do not change `.editorconfig`. The challenge README explicitly asks candidates to leave it unchanged.

## Delivery Practices

Use the assessment guidance files as practical engineering guidance, not as slogans.

Work with this mindset:

- Start from the requirements before introducing architecture.
- Keep the solution small, correct, explainable, and production-aware.
- Prefer boring, readable code over clever abstractions.
- Make each design choice easy to defend in a review.
- Think like the merchant using the API: responses should be predictable, safe, and clear.
- Think like the operator running the API: failures should be logged safely and handled intentionally.
- Think like the reviewer: the solution should be easy to understand in a short assessment review.
- Document honest tradeoffs instead of pretending the solution is production-complete.

Apply these principles across the whole implementation:

- Customer first: provide a predictable API and never expose sensitive card data.
- Run lean: avoid frameworks, patterns, or services that do not directly support the requirements.
- No room for approximation: implement validation rules precisely and cover boundary cases with tests.
- Be the owner: handle bank failures, missing payments, and invalid requests deliberately.
- Talk straight: document assumptions, limitations, and future improvements clearly.
- Testing mindset: add tests as behavior becomes defined, not as a final cleanup step.
- Observability: log useful lifecycle events without logging secrets.
- Packaging and hosting: make the API easy to run locally and optionally with Docker.

## API Design

Keep the API small, resource-oriented, and explicit about outcomes.

Expose only the endpoints needed for the challenge plus optional health:

```text
POST /payments
GET /payments/{id}
GET /health
```

The starter controller currently uses the ASP.NET boilerplate route style `api/[controller]`. Replace that route with the explicit resource route for this solution:

```csharp
[Route("payments")]
```

Update existing tests that call `/api/Payments/...` so they call `/payments/...` instead. The intended API contract is `/payments`, not `/api/Payments`.

Use HTTP status codes for API/transport outcomes and payment `status` values for payment outcomes.

Recommended status mapping:

- `201 Created` for processed payments that were sent to the bank and stored, whether `Authorized` or `Declined`.
- `400 Bad Request` for gateway validation failures with payment status `Rejected`.
- `404 Not Found` when retrieving an unknown payment id.
- `502 Bad Gateway` for bank simulator failures.
- `200 OK` for successful payment retrieval.
- `200 OK` for health.

Prefer simple JSON contracts with camelCase external property names:

```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

Successful processed payment response:

```json
{
  "id": "0f8fad5b-d9cb-469f-a165-70867728950e",
  "status": "Authorized",
  "lastFourCardDigits": "8877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100
}
```

Rejected validation response:

```json
{
  "status": "Rejected",
  "errors": [
    {
      "code": "InvalidCardNumber",
      "message": "Card number must contain 14 to 19 numeric characters."
    }
  ]
}
```

Rules:

- Do not expose full card number.
- Do not expose CVV.
- Do not expose raw bank response details unless required.
- Do not expose bank authorization code unless the requirements are changed to require it.
- Do not leak internal exception names or stack traces in API responses.
- Avoid action-style routes such as `POST /process-payment`.
- Keep response shapes shallow and easy to read.
- When adding or changing an API endpoint, update the Postman collection/scripts in the same task. Create `postman/PaymentGateway.postman_collection.json` if it does not exist.

## Architecture

Use a lightweight layered structure:

```text
src/PaymentGateway.Api/
  Api/
    Controllers/
    Contracts/
  Application/
    Payments/
      Interfaces/
      Models/
  Domain/
    Payments/
  Infrastructure/
    Bank/
    Persistence/
```

Layer responsibilities:

- `Api`: controllers, HTTP request/response contracts, status code mapping, and dependency registration.
- `Application`: payment use cases and validation orchestration. Put application interfaces in `Interfaces/` and commands/results/application models in `Models/`.
- `Domain`: payment model, payment status, and payment-specific rules that are not HTTP or infrastructure concerns.
- `Infrastructure`: acquiring bank HTTP client and in-memory payment repository implementation.

Prefer one application service for this challenge:

```csharp
public interface IPaymentsService
{
    Task<ProcessPaymentResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken cancellationToken);
    Task<GetPaymentResult?> GetAsync(Guid paymentId, CancellationToken cancellationToken);
}
```

Use interfaces for meaningful boundaries:

- `IPaymentsService`
- `IPaymentsRepository`
- `IAcquiringBankClient`

Avoid unnecessary ceremony such as CQRS, MediatR, generic repositories, unit-of-work abstractions, mapping libraries, Kubernetes, Helm, or cloud deployment templates.

## Data Safety

Treat payment data as sensitive.

Rules:

- Never persist CVV.
- Never persist the full card number.
- Never return the full card number.
- Store and return only the last four card digits.
- Do not log raw request bodies.
- Do not log raw bank request or response bodies.
- Do not log full card numbers or CVV values.

Safe values for responses and logs include:

- `PaymentId`
- `Status`
- `Currency`
- `Amount`
- `LastFourCardDigits`
- `ValidationErrorCodes`
- `BankStatusCode`
- `ElapsedMilliseconds`

## Validation

Validate requests before calling the bank simulator.

Gateway-side invalid requests should produce a `Rejected` result and must not call the bank.

Validate:

- Card number is required, numeric, and 14-19 characters long.
- Expiry month is required and between 1 and 12.
- Expiry year is required.
- Expiry month/year combination is not expired. Treat cards as valid through the end of the expiry month, so the current month/year is valid until that month ends.
- Currency is required, 3 characters, and one of the supported ISO currency codes: `GBP`, `USD`, or `EUR`.
- Amount is required and a positive integer in minor currency units.
- CVV is required, numeric, and 3-4 characters long.

Rejected payments should not be stored because no payment was created.

## Bank Simulator

Integrate with the provided acquiring bank simulator through `IAcquiringBankClient`.

The simulator endpoint is:

```text
POST http://localhost:8080/payments
```

Make the bank base URL configurable:

```json
{
  "BankSimulator": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

Map bank responses as follows:

- `authorized: true` -> `Authorized`
- `authorized: false` -> `Declined`
- simulator unavailable or unexpected bank failure -> explicit application failure result, no payment stored, and API response `502 Bad Gateway`

## Observability

Use built-in `ILogger<T>` with structured logging.

Log lifecycle events:

- Payment processing started.
- Payment request rejected.
- Bank request completed.
- Bank request failed.
- Payment stored.
- Payment retrieved.
- Payment not found.

Include bank call elapsed time. Do not add heavy observability dependencies unless requested.

Suggested safe log examples:

```csharp
logger.LogInformation(
    "Payment processing started for currency {Currency} and amount {Amount}",
    command.Currency,
    command.Amount);

logger.LogInformation(
    "Payment {PaymentId} processed with status {Status}",
    payment.Id,
    payment.Status);

logger.LogWarning(
    "Payment request rejected with {ErrorCount} validation errors: {ValidationErrorCodes}",
    errors.Count,
    errors.Select(error => error.Code));

logger.LogInformation(
    "Acquiring bank responded with {StatusCode} in {ElapsedMilliseconds}ms",
    statusCode,
    elapsedMilliseconds);
```

## Packaging and Hosting

Provide both local and Docker-based ways to run the solution.

Required local path:

```bash
dotnet restore
dotnet test
dotnet run --project src/PaymentGateway.Api
docker-compose up
```

Keep the existing `docker-compose.yml` for the bank simulator and do not change the `imposters/` directory.

Add an API `Dockerfile` only if it remains simple and optional. The API must still be runnable without Docker.

If adding Docker support for the API:

- Use configuration to set the bank simulator URL.
- Document the environment variable override:

```text
BankSimulator__BaseUrl=http://bank-simulator:8080
```

Add a simple health endpoint or ASP.NET Core health checks if useful. Do not add Kubernetes, Helm, or cloud-specific hosting files.

## GitHub CI

If adding CI, keep it small and focused on fast verification.

Recommended workflow path:

```text
.github/workflows/ci.yml
```

Run on:

- `push`
- `pull_request`

Recommended steps:

- Check out the repository.
- Set up the required .NET SDK.
- Run `dotnet restore PaymentGateway.sln`.
- Run `dotnet build PaymentGateway.sln --configuration Release --no-restore`.
- Run `dotnet test PaymentGateway.sln --configuration Release --no-build --verbosity normal`.

Do not make the bank simulator or Docker Compose a CI dependency unless tests explicitly require it. Prefer application and API tests that use fakes or test doubles so CI remains fast and reliable.

## Testing Approach

Do not leave tests until the end. Add tests as behavior becomes defined.

Use three levels of tests, with most coverage in the application layer.

### Application Service Tests

These are the primary tests because `PaymentsService` owns payment behavior.

Use fake or stub implementations of:

- `IAcquiringBankClient`
- `IPaymentsRepository`

Cover:

- Valid odd-ending card returns `Authorized`.
- Valid even-ending card returns `Declined`.
- Authorized payment is stored.
- Declined payment is stored.
- Stored payment can be retrieved.
- Unknown payment returns not found or `null`.
- Rejected validation result does not call the bank.
- Rejected validation result is not stored.
- Bank unavailable produces an explicit application failure result and does not store a payment.

These tests should be fast and should not use HTTP.

### Validation Tests

Keep validation tests separate if a `PaymentRequestValidator` exists. Otherwise, cover the same cases through `PaymentsService` tests.

Use parameterized tests where they improve readability.

Cover:

- Card number missing.
- Card number length boundaries: 13, 14, 19, and 20 characters.
- Card number contains non-numeric characters.
- Expiry month boundaries: 0, 1, 12, and 13.
- Expiry date in the past.
- Current-month expiry is valid until the end of the expiry month.
- Currency missing.
- Currency is not 3 characters.
- Unsupported currency.
- Supported currencies: `GBP`, `USD`, and `EUR`.
- Amount is zero or negative.
- CVV length boundaries: 2, 3, 4, and 5 characters.
- CVV contains non-numeric characters.

### API Tests

Use a smaller set of API tests to prove HTTP mapping and JSON contracts. Prefer `WebApplicationFactory` if the project setup supports it.

Cover:

- `POST /payments` valid authorized request returns `201 Created` and a safe response body.
- `POST /payments` valid declined request returns `201 Created` and a safe response body.
- `POST /payments` invalid request returns `400 Bad Request`, `Rejected`, and validation errors.
- `GET /payments/{id}` existing payment returns `200 OK`.
- `GET /payments/{id}` unknown payment returns `404 Not Found`.
- Bank unavailable returns `502 Bad Gateway`.
- Response bodies never include `cardNumber` or `cvv`.

### Infrastructure Tests

Keep infrastructure tests minimal.

Cover:

- In-memory repository can store and retrieve a payment.
- In-memory repository returns not found for unknown ids.
- Bank client maps authorized and declined simulator responses correctly if it can be tested without depending on the real simulator.
- Bank client maps simulator failures to the explicit bank-unavailable application failure behavior.

Do not spend time testing the provided bank simulator itself.
Do not test private methods.
Do not assert exact log message strings. Prefer code review for sensitive logging rules unless a simple log capture is already available.

Preferred order:

1. Define domain and application contracts.
2. Add application service tests.
3. Implement application service behavior.
4. Add validation tests.
5. Implement validation.
6. Add infrastructure and API wiring.
7. Add API/integration tests for HTTP mapping.

Important tests:

- Valid odd-ending card returns `Authorized`.
- Valid even-ending card returns `Declined`.
- Invalid requests return `Rejected`.
- Invalid requests do not call the bank.
- Rejected payments are not stored.
- Authorized and declined payments are stored.
- Existing payment can be retrieved.
- Unknown payment returns not found.
- Bank unavailable behavior is explicit.
- Expiry boundary cases are covered.

## Task Plan

Work in small tasks that keep the solution compiling.

Recommended sequence:

1. `Introduce payment gateway layer structure`
   - Create the architecture folders.
   - Move existing files and update namespaces.

2. `Add GitHub CI workflow`
   - Add `.github/workflows/ci.yml`.
   - Run CI on `push` and `pull_request`.
   - Restore, build, and test `PaymentGateway.sln`.
   - Do not depend on Docker Compose or the bank simulator unless tests explicitly require it.

3. `Define payment domain and application contracts`
   - Add `Payment`, `PaymentStatus`, `IPaymentsService`, `IPaymentsRepository`, `IAcquiringBankClient`, commands, and results.

4. `Add payment service behavior tests`
   - Cover authorization, decline, storage, retrieval, and not found behavior using fakes.

5. `Implement payment application service`
   - Implement processing and retrieval orchestration.

6. `Add payment validation tests`
   - Cover validation rules and prove invalid requests do not call the bank.

7. `Reject invalid payment requests before bank processing`
   - Implement validation and rejected results.

8. `Add bank client and in-memory payment storage`
   - Implement infrastructure and dependency injection.

9. `Expose payment processing and retrieval endpoints`
   - Implement `POST /payments` and `GET /payments/{id}` with safe response contracts.

10. `Cover payment API endpoints`
   - Add endpoint tests for authorized, declined, rejected, found, not found, and bank failure mappings.

11. `Document design and add safe payment logging`
    - Add structured logs and README instructions for design, assumptions, local run, Docker run, and tests.

## Documentation

Update the README with:

- Architecture overview.
- Assumptions and tradeoffs.
- Local run instructions.
- Docker run instructions.
- Test instructions.
- Postman collection instructions, once the collection exists.
- Bank simulator instructions.
- Observability and sensitive-data logging rules.
- Future improvements kept out of scope.

## Code Review Checklist

Treat code review as a first-class evaluation step. Review for correctness, clarity, safety, and maintainability.

Ask these questions before considering the solution review-ready:

- Can a reviewer understand the payment processing and retrieval flows in a few minutes?
- Does the implementation meet the two required workflows without adding unnecessary product scope?
- Are all gateway validation rules enforced before calling `IAcquiringBankClient`?
- Do invalid requests return `Rejected` and avoid bank calls?
- Are authorized and declined payments stored and retrievable?
- Are rejected payments excluded from storage?
- Are controllers thin and free of business logic?
- Does `PaymentsService` own application orchestration clearly?
- Are external boundaries represented by meaningful interfaces?
- Is the domain model small and focused on payment state rather than infrastructure concerns?
- Are API routes resource-oriented and response shapes shallow?
- Are Postman requests and scripts updated for every added or changed API endpoint?
- Do HTTP status codes distinguish API outcomes from payment outcomes?
- Are full card numbers and CVV absent from persistence, responses, logs, and documentation examples where they should not appear?
- Are logs structured and useful without exposing sensitive payment data?
- Is bank simulator failure handled explicitly without leaking internal exception details?
- Do tests prove behavior rather than private implementation details?
- Are validation boundary cases covered?
- Are API tests focused on HTTP mapping and safe response contracts?
- Is infrastructure testing minimal and valuable?
- Does the README match the actual implementation?
- Do local run, test, Docker, and CI instructions match reality?
- Is there any abstraction, library, pattern, or folder that does not earn its keep?

Prefer removing unnecessary complexity over defending it. For this challenge, over-engineering is a real review risk.

## Evaluation Checklist

Before considering the implementation complete, verify:

- `dotnet restore` succeeds.
- `dotnet test` succeeds.
- The API starts locally with `dotnet run --project src/PaymentGateway.Api`.
- The bank simulator starts with `docker-compose up`.
- `POST /payments` can return `Authorized` for a valid odd-ending card.
- `POST /payments` can return `Declined` for a valid even-ending card.
- Invalid gateway requests return `Rejected` and do not call the bank.
- Authorized and declined payments are stored.
- Rejected payments are not stored.
- `GET /payments/{id}` returns stored safe payment details.
- `GET /payments/{id}` returns `404 Not Found` for unknown ids.
- Postman collection requests and scripts match the implemented API behavior.
- Bank simulator unavailable behavior returns `502 Bad Gateway`.
- API responses never include CVV.
- API responses never include the full card number.
- Logs never include CVV.
- Logs never include the full card number.
- Bank call timing is logged safely.
- README run instructions match the implemented local workflow.
- README Docker instructions match the implemented Docker workflow, if API Docker support is added.
- GitHub CI restores, builds, and tests the solution if CI is added.
- README assumptions and tradeoffs match the actual implementation.
