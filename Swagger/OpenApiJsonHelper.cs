using System.Text.Json;
using Microsoft.OpenApi.Any;

namespace HospitalMobileAPPApi.Swagger
{
    internal static class OpenApiJsonHelper
    {
        public static IOpenApiAny CreateFromJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            return ToOpenApiAny(document.RootElement);
        }

        private static IOpenApiAny ToOpenApiAny(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ToOpenApiObject(element),
                JsonValueKind.Array => ToOpenApiArray(element),
                JsonValueKind.String => new OpenApiString(element.GetString()),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => new OpenApiLong(longValue),
                JsonValueKind.Number => new OpenApiDouble(element.GetDouble()),
                JsonValueKind.True => new OpenApiBoolean(true),
                JsonValueKind.False => new OpenApiBoolean(false),
                JsonValueKind.Null => new OpenApiNull(),
                _ => new OpenApiString(element.ToString()),
            };
        }

        private static OpenApiObject ToOpenApiObject(JsonElement element)
        {
            var obj = new OpenApiObject();
            foreach (var property in element.EnumerateObject())
            {
                obj[property.Name] = ToOpenApiAny(property.Value);
            }

            return obj;
        }

        private static OpenApiArray ToOpenApiArray(JsonElement element)
        {
            var array = new OpenApiArray();
            foreach (var item in element.EnumerateArray())
            {
                array.Add(ToOpenApiAny(item));
            }

            return array;
        }
    }
}
