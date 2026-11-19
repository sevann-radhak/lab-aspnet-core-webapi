using LabAspNetCoreWebApi.Api;
using LabAspNetCoreWebApi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LabJwtOptions>(builder.Configuration.GetSection(LabJwtOptions.SectionName));
builder.Services.Configure<LabFeatureOptions>(builder.Configuration.GetSection(LabFeatureOptions.SectionName));

builder.Services.AddSingleton<LifetimeRegistry>();
builder.Services.AddTransient<TransientProbe>();
builder.Services.AddScoped<ScopedProbe>();
builder.Services.AddSingleton<SingletonProbe>();
builder.Services.AddSingleton<TokenService>();

LabJwtOptions jwt = builder.Configuration.GetSection(LabJwtOptions.SectionName).Get<LabJwtOptions>()
    ?? new LabJwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "lab-aspnet-core-webapi";
        document.Info.Version = "v1";
        document.Info.Description =
            """
            Order of the pipeline: exception handling, request timing, OpenAPI and Swagger UI (Development only), authentication, authorization, then the endpoint.

            Public operations do not require a token. For GET /lab/secure and GET /lab/admin, call POST /lab/token, copy accessToken, and use Authorize. Paste the token only, without the Bearer prefix.

            A missing or invalid JSON body is a client error (400). An unexpected exception is a server error (500 ProblemDetails). GET /lab/boom is the 500 sample. GET /lab/anti-patterns/blocking returns 200 on purpose and is still a defect.
            """;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Lab JWT from POST /lab/token. Paste accessToken only.",
        };

        DescribeTags(document);
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool requiresAuth = metadata.OfType<IAuthorizeData>().Any()
            && !metadata.OfType<IAllowAnonymous>().Any();
        if (!requiresAuth)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document, externalResource: null)] = [],
        });
        return Task.CompletedTask;
    });
});
builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        Exception? error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        bool isBadRequest = error is BadHttpRequestException;
        int status = isBadRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        ProblemDetails problem = new()
        {
            Status = status,
            Title = isBadRequest
                ? "The request is invalid."
                : "An unexpected error occurred.",
            Detail = isBadRequest
                ? "The request body is missing or is not valid JSON."
                : "Lab global exception middleware returned ProblemDetails.",
            Instance = context.Request.Path,
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json");
    });
});

app.UseMiddleware<RequestTimingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "lab-aspnet-core-webapi v1");
        options.DocumentTitle = "lab-aspnet-core-webapi";
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
        options.DefaultModelsExpandDepth(2);
    });
}

// HTTPS redirection omitted in this teaching lab so local HTTP tests stay simple.
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", "lab-aspnet-core-webapi")))
    .WithName("GetHealth")
    .WithTags("Health")
    .WithSummary("Liveness probe")
    .WithDescription(
        "Returns 200 when this process can serve HTTP. No parameters and no authentication. " +
        "It does not check a database or any downstream dependency. Use it as the first call before the lab endpoints.")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .AllowAnonymous();

app.MapGet("/lab/di", (IServiceScopeFactory scopeFactory) =>
{
    using IServiceScope scope = scopeFactory.CreateScope();
    TransientProbe transientA = scope.ServiceProvider.GetRequiredService<TransientProbe>();
    TransientProbe transientB = scope.ServiceProvider.GetRequiredService<TransientProbe>();
    ScopedProbe scopedA = scope.ServiceProvider.GetRequiredService<ScopedProbe>();
    ScopedProbe scopedB = scope.ServiceProvider.GetRequiredService<ScopedProbe>();
    SingletonProbe singleton = scope.ServiceProvider.GetRequiredService<SingletonProbe>();
    LifetimeRegistry registry = scope.ServiceProvider.GetRequiredService<LifetimeRegistry>();

    return Results.Ok(new DiLifetimeResponse(
        "Transient differs per resolve. Scoped matches inside one scope. Singleton is shared.",
        [transientA.InstanceId, transientB.InstanceId],
        [scopedA.InstanceId, scopedB.InstanceId],
        singleton.InstanceId,
        registry.CreatedCounts));
})
    .WithName("GetDependencyInjectionLifetimes")
    .WithTags("Dependency injection")
    .WithSummary("Compare Transient, Scoped, and Singleton in one scope")
    .WithDescription(
        "No parameters. Resolves each demo service twice inside a single IServiceScope, except Singleton, which is process-wide. " +
        "Call this operation twice. transient ids differ on every resolve. scoped ids match inside one response and change on the next request. " +
        "singleton stays the same, and createdCounts.singleton stays at 1.")
    .Produces<DiLifetimeResponse>(StatusCodes.Status200OK)
    .AllowAnonymous();

app.MapGet("/lab/options", (Microsoft.Extensions.Options.IOptions<LabFeatureOptions> options) =>
    Results.Ok(options.Value))
    .WithName("GetFeatureOptions")
    .WithTags("Configuration")
    .WithSummary("Read the Features section through IOptions")
    .WithDescription(
        "No parameters. Returns LabFeatureOptions bound from the Features section at startup. " +
        "IOptions does not reload appsettings.json while the process is running. " +
        "IOptionsSnapshot and IOptionsMonitor are the reload-aware alternatives and are not used here.")
    .Produces<LabFeatureOptions>(StatusCodes.Status200OK)
    .AllowAnonymous();

app.MapPost("/lab/token", (TokenRequest? request, TokenService tokens) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.UserName))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["userName"] = ["A JSON body with userName is required."],
        });
    }

    string role = string.Equals(request.Role, "admin", StringComparison.OrdinalIgnoreCase)
        ? "admin"
        : "reader";
    string token = tokens.CreateToken(request.UserName, role);
    return Results.Ok(new TokenResponse(token, role));
})
    .WithName("IssueLabToken")
    .WithTags("Authentication")
    .WithSummary("Issue a lab JWT")
    .WithDescription(
        "Body: TokenRequest as application/json. userName is required. role is optional. " +
        "Only admin, compared case-insensitively, is written as the role claim. Null or any other role becomes reader. " +
        "Issuer, audience, and the signing key come from the Jwt configuration section. The key is a public lab placeholder. " +
        "No body, or a blank userName, returns 400 validation errors. Malformed JSON returns 400 ProblemDetails, not 500. " +
        "Copy accessToken into Authorize without the Bearer prefix.")
    .Accepts<TokenRequest>("application/json")
    .Produces<TokenResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .AllowAnonymous();

app.MapGet("/lab/secure", () => Results.Ok(new LabMessageResponse("Authenticated.")))
    .WithName("GetSecure")
    .WithTags("Authentication")
    .WithSummary("Require any valid lab JWT")
    .WithDescription(
        "No parameters. Authentication only: the caller must present a valid Bearer token for this lab's issuer, audience, and signing key. " +
        "Role is not checked. No token, or a token this API cannot validate, returns 401. A reader token and an admin token both return 200.")
    .Produces<LabMessageResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .RequireAuthorization();

app.MapGet("/lab/admin", () => Results.Ok(new LabMessageResponse("Admin policy passed.")))
    .WithName("GetAdmin")
    .WithTags("Authentication")
    .WithSummary("Require the admin role")
    .WithDescription(
        "No parameters. The AdminOnly policy requires an authenticated caller whose role claim is admin. " +
        "No token returns 401. A valid reader token returns 403. An admin token returns 200. " +
        "401 means the API does not know who the caller is. 403 means it does, and the caller is not allowed.")
    .Produces<LabMessageResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .RequireAuthorization("AdminOnly");

app.MapGet("/lab/boom", () =>
{
    throw new InvalidOperationException("Intentional lab failure for ProblemDetails.");
})
    .WithName("GetBoom")
    .WithTags("Errors")
    .WithSummary("Throw an unhandled server exception")
    .WithDescription(
        "No parameters. Throws InvalidOperationException on purpose so the exception middleware can turn it into HTTP 500 ProblemDetails. " +
        "The JSON uses application/problem+json. This path is for unexpected failures. Validation and a missing body stay on 400.")
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .AllowAnonymous();

app.MapGet("/lab/anti-patterns/blocking", () =>
{
    string value = Task.Run(async () =>
    {
        await Task.Delay(10);
        return "prefer await instead of blocking";
    }).GetAwaiter().GetResult();
    return Results.Ok(new BlockingSampleResponse(
        "This endpoint blocks a thread on purpose for teaching. Prefer async all the way.",
        value));
})
    .WithName("GetBlockingAntiPattern")
    .WithTags("Performance")
    .WithSummary("Block a request thread on purpose")
    .WithDescription(
        "No parameters. Returns 200. The handler still calls GetResult() on an asynchronous delay, so a thread stays blocked until the delay finishes. " +
        "Under concurrency that pattern exhausts the thread pool and slows unrelated requests. The fix is to await the work on the endpoint, not to raise the thread-pool minimum.")
    .Produces<BlockingSampleResponse>(StatusCodes.Status200OK)
    .AllowAnonymous();

app.Run();

static void DescribeTags(OpenApiDocument document)
{
    Dictionary<string, string> descriptions = new(StringComparer.Ordinal)
    {
        ["Health"] = "Process liveness. No authentication and no dependency checks.",
        ["Dependency injection"] = "Transient, Scoped, and Singleton resolved inside one request scope.",
        ["Configuration"] = "Options binding. This lab uses IOptions, a startup snapshot.",
        ["Authentication"] = "Lab JWT issuance, authentication (401), and the admin role policy (403).",
        ["Errors"] = "Unexpected failures mapped to ProblemDetails.",
        ["Performance"] = "Intentional thread-pool defect. The HTTP status is still 200.",
    };

    if (document.Tags is null)
    {
        return;
    }

    foreach (OpenApiTag tag in document.Tags.OfType<OpenApiTag>())
    {
        if (tag.Name is not null && descriptions.TryGetValue(tag.Name, out string? description))
        {
            tag.Description = description;
        }
    }
}

public partial class Program;
