using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HospitalMobileAPPApi.Swagger
{
    public class ApiDocumentationOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var operationId = context.ApiDescription.ActionDescriptor.RouteValues["action"];
            var controllerName = context.ApiDescription.ActionDescriptor.RouteValues["controller"];
            var key = $"{controllerName}_{operationId}";

            if (!ApiDocumentationRegistry.Operations.TryGetValue(key, out var doc))
            {
                return;
            }

            operation.Summary = doc.Summary;
            operation.Description = doc.Description;

            foreach (var parameter in operation.Parameters)
            {
                if (doc.ParameterDescriptions.TryGetValue(parameter.Name, out var paramDescription))
                {
                    parameter.Description = paramDescription;
                }
            }

            if (!string.IsNullOrWhiteSpace(doc.RequestExample))
            {
                try
                {
                    operation.RequestBody ??= new OpenApiRequestBody
                    {
                        Required = true,
                        Content = new Dictionary<string, OpenApiMediaType>(),
                    };

                    if (!operation.RequestBody.Content.ContainsKey("application/json"))
                    {
                        operation.RequestBody.Content["application/json"] = new OpenApiMediaType();
                    }

                    operation.RequestBody.Content["application/json"].Example =
                        OpenApiJsonHelper.CreateFromJson(doc.RequestExample);
                }
                catch
                {
                    // Invalid example JSON must never break /swagger/v1/swagger.json.
                }
            }

            if (!string.IsNullOrWhiteSpace(doc.ResponseExample))
            {
                try
                {
                    var response = operation.Responses.TryGetValue("200", out var okResponse)
                        ? okResponse
                        : new OpenApiResponse { Description = "Success" };

                    response.Content ??= new Dictionary<string, OpenApiMediaType>();
                    if (!response.Content.ContainsKey("application/json"))
                    {
                        response.Content["application/json"] = new OpenApiMediaType();
                    }

                    response.Content["application/json"].Example =
                        OpenApiJsonHelper.CreateFromJson(doc.ResponseExample);

                    operation.Responses["200"] = response;
                }
                catch
                {
                    // Invalid example JSON must never break /swagger/v1/swagger.json.
                }
            }
        }
    }
}
