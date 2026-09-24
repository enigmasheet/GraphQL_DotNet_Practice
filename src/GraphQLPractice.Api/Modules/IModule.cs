namespace GraphQLPractice.Api.Modules;

/// <summary>
/// A vertical slice of the application. Each module owns its GraphQL types and
/// its EF Core entity configuration.
/// </summary>
public interface IModule
{
    string Name { get; }

    /// <summary>Register module-specific services.</summary>
    void Register(IServiceCollection services);
}
