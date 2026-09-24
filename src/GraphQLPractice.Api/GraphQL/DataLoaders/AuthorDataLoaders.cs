using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.GraphQL.DataLoaders;

internal static class AuthorDataLoaders
{
    // Batch loader: one query for all requested author ids (deduplicated).
    [DataLoader]
    public static async Task<IReadOnlyDictionary<int, Author>> GetAuthorByIdAsync(
        IReadOnlyList<int> ids,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.Authors.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
    }
}
