namespace GraphQLPractice.Api.Modules.Posts;

internal sealed class PostsModule : IModule
{
    public string Name => "Posts";

    public void Register(IServiceCollection services)
    {
        // No module-specific services yet.
    }
}
