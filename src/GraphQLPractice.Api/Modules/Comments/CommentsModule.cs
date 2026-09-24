namespace GraphQLPractice.Api.Modules.Comments;

internal sealed class CommentsModule : IModule
{
    public string Name => "Comments";

    public void Register(IServiceCollection services)
    {
        // No module-specific services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCommentEndpoints();
    }
}
