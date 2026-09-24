using GraphQLPractice.Api.GraphQL.DataLoaders;
using GraphQLPractice.Api.Models;

namespace GraphQLPractice.Api.GraphQL.Types;

[ObjectType<Author>]
public static partial class AuthorNode
{
    // Resolved by a group DataLoader: one query for all authors' posts in the request.
    public static async Task<IEnumerable<BlogPost>> GetPostsAsync(
        [Parent] Author author,
        IPostsByAuthorIdDataLoader postsByAuthorId,
        CancellationToken ct
    ) => await postsByAuthorId.LoadAsync(author.Id, ct) ?? [];
}
