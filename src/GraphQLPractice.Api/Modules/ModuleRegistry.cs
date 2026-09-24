using GraphQLPractice.Api.Modules.Authors;
using GraphQLPractice.Api.Modules.Comments;
using GraphQLPractice.Api.Modules.Posts;
using GraphQLPractice.Api.Modules.Tags;

namespace GraphQLPractice.Api.Modules;

/// <summary>
/// Explicit list of the modules that make up this modular monolith. The host
/// iterates it to register services and map endpoints.
/// </summary>
public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> Modules { get; } =
    [new AuthorsModule(), new PostsModule(), new CommentsModule(), new TagsModule()];
}
