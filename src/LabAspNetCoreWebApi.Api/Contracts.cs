using System.ComponentModel;

namespace LabAspNetCoreWebApi.Api;

public sealed record HealthResponse(
    [property: Description("ok when the process accepted the HTTP request.")]
    string Status,
    [property: Description("Lab identifier. This is not an environment name.")]
    string Lab);

public sealed record CreatedCounts(
    [property: Description("Transient constructors executed in this process.")]
    int Transient,
    [property: Description("Scoped constructors executed in this process.")]
    int Scoped,
    [property: Description("Singleton constructors executed in this process. Stays at 1 after the first resolve.")]
    int Singleton);

public sealed record DiLifetimeResponse(
    [property: Description("How to read the ids in this payload.")]
    string Note,
    [property: Description("Two resolves of a transient service inside one scope. The ids differ.")]
    Guid[] Transient,
    [property: Description("Two resolves of a scoped service inside one scope. The ids match.")]
    Guid[] Scoped,
    [property: Description("The process-wide singleton id. Repeat the call to see that it does not change.")]
    Guid Singleton,
    CreatedCounts CreatedCounts);

public sealed record TokenRequest(
    [property: Description("Required. Stored in the subject (sub) and name claims.")]
    string UserName,
    [property: Description("Optional. Only the value admin is honored, case-insensitive. Null or any other value becomes reader.")]
    string? Role);

public sealed record TokenResponse(
    [property: Description("JWT to paste into Swagger Authorize. Do not include the Bearer prefix.")]
    string AccessToken,
    [property: Description("Role actually written into the token: admin or reader.")]
    string Role);

public sealed record LabMessageResponse(
    [property: Description("Confirms which check passed.")]
    string Message);

public sealed record BlockingSampleResponse(
    [property: Description("Why this endpoint is a defect even though it returns 200.")]
    string Warning,
    [property: Description("Value produced by the blocked task.")]
    string Value);
