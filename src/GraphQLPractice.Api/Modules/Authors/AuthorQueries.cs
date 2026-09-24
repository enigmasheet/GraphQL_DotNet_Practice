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
    ) =>
        await db
            .Authors.With(query, sort => sort.IfEmpty(s => s.AddAscending(a => a.Id)))
            .ToPageAsync(pagingArgs, ct);

    [GraphQLDeprecated("Use the node(id: ID!) field instead.")]
    public static Task<Author?> GetAuthorByIdAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Authors.FirstOrDefaultAsync(a => a.Id == id, ct);

    // Node resolver for the Relay global object identification pattern.
    [NodeResolver]
    [GraphQLIgnore]
    public static Task<Author?> ResolveAuthorAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Authors.FirstOrDefaultAsync(a => a.Id == id, ct);
}
