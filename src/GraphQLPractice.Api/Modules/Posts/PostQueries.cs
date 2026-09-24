using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using GreenDonut.Data;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Posts;

[QueryType]
public static partial class PostQueries
{
    [UseConnection(IncludeTotalCount = true, MaxPageSize = 50)]
    [UseFiltering]
    [UseSorting]
    public static async Task<PageConnection<BlogPost>> GetPostsAsync(
        PagingArguments pagingArgs,
        QueryContext<BlogPost> query,
        AppDbContext db,
        CancellationToken ct
    ) => await db.BlogPosts.With(query).ToPageAsync(pagingArgs, ct);

    public static Task<BlogPost?> GetPostByIdAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
}
