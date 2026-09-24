namespace GraphQLPractice.Api.Modules.Tags;

internal sealed class TagsModule : IModule
{
    public string Name => "Tags";

    public void Register(IServiceCollection services)
    {
        // No module-specific services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTagEndpoints();
    }
}
