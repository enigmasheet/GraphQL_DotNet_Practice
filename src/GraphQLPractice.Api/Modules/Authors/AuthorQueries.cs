using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using GreenDonut.Data;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Authors;

[QueryType]
public static partial class AuthorQueries
{
    [UseConnection(IncludeTotalCount = true, MaxPageSize = 50)]
    [UseFiltering]
    [UseSorting]
    public static async Task<PageConnection<Author>> GetAuthorsAsync(
        PagingArguments pagingArgs,
        QueryContext<Author> query,
        AppDbContext db,
        CancellationToken ct
    ) => await db.Authors.With(query).ToPageAsync(pagingArgs, ct);

    public static Task<Author?> GetAuthorByIdAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Authors.FirstOrDefaultAsync(a => a.Id == id, ct);
}
