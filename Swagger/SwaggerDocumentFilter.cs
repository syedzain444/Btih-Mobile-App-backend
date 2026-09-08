using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HospitalMobileAPPApi.Swagger
{
    public class SwaggerDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Info.Description = """
                **Bahria Town International Hospital — Patient Mobile App API**

                ## Authentication
                1. Call `POST /api/Auth/login` with contact number and password.
                2. Copy the `token` from the response.
                3. Click **Authorize** (top right) and enter: `Bearer {your-token}`

                ## Public endpoints (no token)
                - Auth (login, OTP, verify phone)
                - Doctor list, schedule, specializations
                - Guest appointment booking (`POST /api/Patient/insertchallan`)
                - Password reset after OTP (`POST /api/Patient/updatePassword`)

                ## Typical mobile flows
                - **Login** → register push token → use patient APIs with Bearer token
                - **Forgot password** → verifyPhoneNo → send-otp → verify-otp → updatePassword
                - **Reports** → list endpoints → GenerateReport PDF with PAT_DIAG_ID or visit id

                ## Deployment note
                Swagger is enabled when `EnableSwagger=true` in config or in Development environment.
                """;

            foreach (var tag in swaggerDoc.Tags ?? Enumerable.Empty<OpenApiTag>())
            {
                if (ApiDocumentationRegistry.TagDescriptions.TryGetValue(tag.Name, out var description))
                {
                    tag.Description = description;
                }
            }
        }
    }
}
