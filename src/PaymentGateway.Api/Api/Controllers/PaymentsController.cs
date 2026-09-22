using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Api.Contracts.Requests;
using PaymentGateway.Api.Api.Contracts.Responses;
using PaymentGateway.Api.Application.Payments.Exceptions;
using PaymentGateway.Api.Application.Payments.Interfaces;
using PaymentGateway.Api.Application.Payments.Models;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Api.Controllers;

[Route("payments")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentsService _paymentsService;

    public PaymentsController(IPaymentsService paymentsService)
    {
        _paymentsService = paymentsService;
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var merchantId = Request.Headers["X-Merchant-Id"].ToString();
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();

            var result = await _paymentsService.ProcessAsync(
                new ProcessPaymentCommand(
                    merchantId,
                    idempotencyKey,
                    request.CardNumber,
                    request.ExpiryMonth,
                    request.ExpiryYear,
                    request.Currency,
                    request.Amount,
                    request.Cvv),
                cancellationToken);

            if (result.Status == PaymentStatus.Rejected)
            {
                return new BadRequestObjectResult(new PostPaymentRejectedResponse
                {
                    Status = result.Status,
                    Errors = result.Errors
                });
            }

            var response = ToPostPaymentResponse(result);

            return result.IsNewPayment
                ? new CreatedResult($"/payments/{response.Id}", response)
                : new OkObjectResult(response);
        }
        catch (IdempotencyKeyConflictException)
        {
            return new ConflictResult();
        }
        catch (AcquiringBankUnavailableException)
        {
            return new StatusCodeResult(StatusCodes.Status502BadGateway);
        }
        catch (AcquiringBankOutcomeUnknownException)
        {
            return new StatusCodeResult(StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetPaymentResponse>> GetPaymentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var merchantId = Request.Headers["X-Merchant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            return new BadRequestResult();
        }

        var payment = await _paymentsService.GetAsync(id, merchantId, cancellationToken);
        if (payment is null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(new GetPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            LastFourCardDigits = payment.LastFourCardDigits,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        });
    }

    private static PostPaymentResponse ToPostPaymentResponse(ProcessPaymentResult result)
    {
        if (result.Id is null ||
            result.LastFourCardDigits is null ||
            result.ExpiryMonth is null ||
            result.ExpiryYear is null ||
            result.Currency is null ||
            result.Amount is null)
        {
            throw new InvalidOperationException("Processed payment result is missing payment details.");
        }

        return new PostPaymentResponse
        {
            Id = result.Id.Value,
            Status = result.Status,
            LastFourCardDigits = result.LastFourCardDigits,
            ExpiryMonth = result.ExpiryMonth.Value,
            ExpiryYear = result.ExpiryYear.Value,
            Currency = result.Currency,
            Amount = result.Amount.Value
        };
    }
}
