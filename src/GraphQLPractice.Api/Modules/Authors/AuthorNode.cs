using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using GreenDonut.Data;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Authors;

[ObjectType<Author>]
public static partial class AuthorNode
{
    [UseConnection(IncludeTotalCount = true, MaxPageSize = 10)]
    [UseFiltering]
    [UseSorting]
    public static async Task<PageConnection<BlogPost>> GetPostsAsync(
        [Parent(requires: nameof(Author.Id))] Author author,
        PagingArguments pagingArgs,
        QueryContext<BlogPost> query,
        AppDbContext db,
        CancellationToken ct
    ) =>
        await db
            .BlogPosts.Where(p => p.AuthorId == author.Id)
            .With(query)
            .ToPageAsync(pagingArgs, ct);
}
