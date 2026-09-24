namespace GraphQLPractice.Api.Modules.Authors;

internal sealed class AuthorsModule : IModule
{
    public string Name => "Authors";

    public void Register(IServiceCollection services)
    {
        // No module-specific services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthorEndpoints();
    }
}
