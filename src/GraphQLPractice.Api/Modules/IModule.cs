namespace GraphQLPractice.Api.Modules;

/// <summary>
/// A vertical slice of the application. Each module owns its GraphQL types, its
/// Minimal API endpoints and its EF Core entity configuration.
/// </summary>
public interface IModule
{
    string Name { get; }

    /// <summary>Register module-specific services.</summary>
    void Register(IServiceCollection services);

    /// <summary>Map the module's Minimal API endpoints.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
