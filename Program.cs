using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Data;
using HospitalMobileAPPApi.Filters;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Repository;
using HospitalMobileAPPApi.Services;
using HospitalMobileAPPApi.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection(AuthSettings.SectionName));
builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection(SmsSettings.SectionName));
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection(SecuritySettings.SectionName));
builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection(RateLimitSettings.SectionName));
builder.Services.Configure<MessagingSettings>(builder.Configuration.GetSection(MessagingSettings.SectionName));
builder.Services.Configure<ReminderSettings>(builder.Configuration.GetSection(ReminderSettings.SectionName));

DataProtectionConfigurator.ConfigureDataProtection(builder);

builder.Services.AddMemoryCache();

var rateLimitSettings = builder.Configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitSettings.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds),
                QueueLimit = 0,
            }));
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("HMISConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IPushNotificationRepository, PushNotificationRepository>();
builder.Services.AddScoped<INotificationHistoryService, NotificationHistoryService>();
builder.Services.AddScoped<INotificationHistoryRepository, NotificationHistoryRepository>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IBillingRepository, BillingRepository>();
builder.Services.AddScoped<ITrustedDeviceService, TrustedDeviceService>();
builder.Services.AddScoped<ITrustedDeviceRepository, TrustedDeviceRepository>();
builder.Services.AddScoped<IGuestService, GuestService>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddSingleton<IFcmPushSender, FcmPushSender>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IMessagingRepository, MessagingRepository>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IMedicationRepository, MedicationRepository>();
builder.Services.AddScoped<IMedicationService, MedicationService>();
builder.Services.AddScoped<IReminderRepository, ReminderRepository>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IMobilePortalSchemaService, MobilePortalSchemaService>();
builder.Services.AddHostedService<MedicationReminderBackgroundService>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey is not configured. Set JwtSettings:SecretKey or environment variable JwtSettings__SecretKey.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<OracleExceptionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BTIH Hospital Mobile App API",
        Version = "v1",
        Description = "Bahria Town International Hospital patient portal API.",
        Contact = new OpenApiContact
        {
            Name = "BTIH IT Support",
            Email = "it@btkhospital.com",
        },
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Bearer token. Format: Bearer {token from /api/Auth/login}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    options.OperationFilter<AuthorizeCheckOperationFilter>();
    options.OperationFilter<ApiDocumentationOperationFilter>();
    options.DocumentFilter<SwaggerDocumentFilter>();
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));
});

var app = builder.Build();

var hmisConnection = app.Configuration.GetConnectionString("HMISConnection");
if (string.IsNullOrWhiteSpace(hmisConnection))
{
    app.Logger.LogWarning("HMISConnection is empty. Set ConnectionStrings:HMISConnection or environment variable ConnectionStrings__HMISConnection.");
}
else
{
    try
    {
        await using var testConn = new Oracle.ManagedDataAccess.Client.OracleConnection(hmisConnection);
        await testConn.OpenAsync();
        app.Logger.LogInformation("HMIS Oracle connection verified at startup.");

        try
        {
            using var scope = app.Services.CreateScope();
            var schemaService = scope.ServiceProvider.GetRequiredService<IMobilePortalSchemaService>();
            var missing = await schemaService.GetMissingTablesAsync();
            if (missing.Count > 0)
            {
                app.Logger.LogWarning(
                    "Missing mobile portal tables: {Tables}. Run Docs/MOBILE_PORTAL_TABLES.sql (or Docs/MOBILE_MESSAGING_TABLES.sql for messaging only).",
                    string.Join(", ", missing));
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not verify mobile portal schema at startup.");
        }
    }
    catch (Exception ex)
    {
        if (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var message, out _))
        {
            app.Logger.LogError("HMIS Oracle connection failed at startup: {Message}", message);
        }
        else
        {
            app.Logger.LogError(ex, "HMIS Oracle connection failed at startup.");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

var securitySettings = app.Configuration.GetSection(SecuritySettings.SectionName).Get<SecuritySettings>() ?? new SecuritySettings();
if (securitySettings.RequireHttps || !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var enableSwagger = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hospital Mobile APP API v1");
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
