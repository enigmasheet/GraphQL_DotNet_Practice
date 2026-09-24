using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using GreenDonut.Data;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Tags;

[QueryType]
public static partial class TagQueries
{
    [UseConnection(IncludeTotalCount = true, MaxPageSize = 50)]
    [UseFiltering]
    [UseSorting]
    public static async Task<PageConnection<Tag>> GetTagsAsync(
        PagingArguments pagingArgs,
        QueryContext<Tag> query,
        AppDbContext db,
        CancellationToken ct
    ) =>
        await db
            .Tags.With(query, sort => sort.IfEmpty(s => s.AddAscending(t => t.Id)))
            .ToPageAsync(pagingArgs, ct);

    // Node resolver for the Relay global object identification pattern.
    [NodeResolver]
    [GraphQLIgnore]
    public static Task<Tag?> ResolveTagAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
}
