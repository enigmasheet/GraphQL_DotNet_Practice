using GraphQLPractice.Api.GraphQL.DataLoaders;
using GraphQLPractice.Api.Models;

namespace GraphQLPractice.Api.GraphQL.Types;

[ObjectType<Comment>]
public static partial class CommentNode
{
    public static async Task<Author> GetAuthorAsync(
        [Parent] Comment comment,
        IAuthorByIdDataLoader authorById,
        CancellationToken ct
    ) => await authorById.LoadRequiredAsync(comment.AuthorId, ct);

    // Reuses the same batch loader as BlogPostNode; the loader cache serves repeated keys.
    public static async Task<BlogPost> GetPostAsync(
        [Parent] Comment comment,
        IPostByIdDataLoader postById,
        CancellationToken ct
    ) => await postById.LoadRequiredAsync(comment.BlogPostId, ct);
}
