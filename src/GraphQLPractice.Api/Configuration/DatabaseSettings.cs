namespace GraphQLPractice.Api.Configuration;

/// <summary>
/// Strongly-typed view of the <c>ConnectionStrings</c> section, bound through the
/// options pattern instead of reading <c>IConfiguration</c> at the call site.
/// </summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "ConnectionStrings";

    public string? Postgres { get; set; }
}
