using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentGateway.Api.Api.Swagger;

public sealed class PaymentHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var relativePath = context.ApiDescription.RelativePath;
        if (relativePath is null || !relativePath.StartsWith("payments", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddHeader(operation, "X-Merchant-Id");

        if (string.Equals(context.ApiDescription.HttpMethod, HttpMethods.Post, StringComparison.OrdinalIgnoreCase))
        {
            AddHeader(operation, "Idempotency-Key");
        }
    }

    private static void AddHeader(OpenApiOperation operation, string name)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        if (operation.Parameters.Any(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase) &&
                parameter.In == ParameterLocation.Header))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = "string"
            }
        });
    }
}
