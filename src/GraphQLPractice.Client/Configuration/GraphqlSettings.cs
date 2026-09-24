namespace GraphQLPractice.Client.Configuration;

/// <summary>
/// Strongly-typed view of the <c>Graphql</c> section of <c>wwwroot/appsettings.json</c>.
/// The WebSocket URL is derived from <see cref="Url"/> so the two cannot drift apart.
/// </summary>
public sealed class GraphqlSettings
{
    public const string SectionName = "Graphql";

    public string Url { get; set; } = "http://localhost:5100/graphql";

    public string WebSocketUrl =>
        Url.Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
}
