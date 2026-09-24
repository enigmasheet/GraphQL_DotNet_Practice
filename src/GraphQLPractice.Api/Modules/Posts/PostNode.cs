using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using GraphQLPractice.Api.Modules.Authors;
using GreenDonut.Data;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Posts;

[ObjectType<BlogPost>]
public static partial class PostNode
{
    public static async Task<Author> GetAuthorAsync(
        [Parent(requires: nameof(BlogPost.AuthorId))] BlogPost post,
        IAuthorByIdDataLoader authorById,
        CancellationToken ct
    ) => await authorById.LoadRequiredAsync(post.AuthorId, ct);

    [UseConnection(IncludeTotalCount = true, MaxPageSize = 50)]
    [UseFiltering]
    [UseSorting]
    public static async Task<PageConnection<Comment>> GetCommentsAsync(
        [Parent(requires: nameof(BlogPost.Id))] BlogPost post,
        PagingArguments pagingArgs,
        QueryContext<Comment> query,
        AppDbContext db,
        CancellationToken ct
    ) =>
        await db
            .Comments.Where(c => c.BlogPostId == post.Id)
            .With(query, sort => sort.IfEmpty(s => s.AddAscending(c => c.Id)))
            .ToPageAsync(pagingArgs, ct);

    [UseConnection(IncludeTotalCount = true, MaxPageSize = 20)]
    [UseFiltering]
    [UseSorting]
    public static async Task<PageConnection<Tag>> GetTagsAsync(
        [Parent(requires: nameof(BlogPost.Id))] BlogPost post,
        PagingArguments pagingArgs,
        QueryContext<Tag> query,
        AppDbContext db,
        CancellationToken ct
    ) =>
        await db
            .Tags.Where(t => t.Posts.Any(p => p.Id == post.Id))
            .With(query, sort => sort.IfEmpty(s => s.AddAscending(t => t.Id)))
            .ToPageAsync(pagingArgs, ct);
}
