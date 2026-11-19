using System.ComponentModel;

namespace LabAspNetCoreWebApi.Api;

public sealed class LabJwtOptions
{
    public const string SectionName = "Jwt";

    [Description("Value the API accepts in the token iss claim.")]
    public string Issuer { get; set; } = "lab-aspnet-core-webapi";

    [Description("Value the API accepts in the token aud claim.")]
    public string Audience { get; set; } = "lab-clients";

    [Description("HMAC key for this lab only. Never reuse it outside the repository.")]
    public string SigningKey { get; set; } = "lab-only-dev-signing-key-min-32-chars!";
}

public sealed class LabFeatureOptions
{
    public const string SectionName = "Features";

    [Description("Bound from configuration. The timing middleware currently logs every request and does not read this flag.")]
    public bool EnableVerboseTiming { get; set; } = true;

    [Description("Sample value used to show that IOptions returns the startup snapshot.")]
    public string SampleFlag { get; set; } = "wave-1";
}
