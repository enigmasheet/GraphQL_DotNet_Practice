using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using GraphQLPractice.Api.Modules.Authors;
using GraphQLPractice.Api.Modules.Posts;

namespace GraphQLPractice.Api.Modules.Comments;

[ObjectType<Comment>]
public static partial class CommentNode
{
    public static async Task<Author> GetAuthorAsync(
        [Parent] Comment comment,
        IAuthorByIdDataLoader authorById,
        CancellationToken ct
    ) => await authorById.LoadRequiredAsync(comment.AuthorId, ct);

    // Reuses the same batch loader as PostNode; the loader cache serves repeated keys.
    public static async Task<BlogPost> GetPostAsync(
        [Parent] Comment comment,
        IPostByIdDataLoader postById,
        CancellationToken ct
    ) => await postById.LoadRequiredAsync(comment.BlogPostId, ct);
}
