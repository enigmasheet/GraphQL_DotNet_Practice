using GraphQLPractice.Api.GraphQL.DataLoaders;
using GraphQLPractice.Api.Models;

namespace GraphQLPractice.Api.GraphQL.Types;

[ObjectType<BlogPost>]
public static partial class BlogPostNode
{
    public static async Task<Author> GetAuthorAsync(
        [Parent] BlogPost post,
        IAuthorByIdDataLoader authorById,
        CancellationToken ct
    ) => await authorById.LoadRequiredAsync(post.AuthorId, ct);

    public static async Task<IEnumerable<Comment>> GetCommentsAsync(
        [Parent] BlogPost post,
        ICommentsByPostIdDataLoader commentsByPostId,
        CancellationToken ct
    ) => await commentsByPostId.LoadAsync(post.Id, ct) ?? [];

    public static async Task<IEnumerable<Tag>> GetTagsAsync(
        [Parent] BlogPost post,
        ITagsByPostIdDataLoader tagsByPostId,
        CancellationToken ct
    ) => await tagsByPostId.LoadAsync(post.Id, ct) ?? [];
}
