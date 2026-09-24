namespace GraphQLPractice.Api.Configuration;

/// <summary>
/// Strongly-typed view of the <c>Cors</c> section.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}
