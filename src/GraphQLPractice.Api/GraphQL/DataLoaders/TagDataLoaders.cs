using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.GraphQL.DataLoaders;

internal static class TagDataLoaders
{
    // Group loader across the many-to-many join: many tags per post.
    [DataLoader]
    public static async Task<ILookup<int, Tag>> GetTagsByPostIdAsync(
        IReadOnlyList<int> postIds,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db
            .BlogPosts.Where(p => postIds.Contains(p.Id))
            .SelectMany(p => p.Tags.Select(t => new { PostId = p.Id, Tag = t }))
            .ToListAsync(ct);

        return rows.ToLookup(row => row.PostId, row => row.Tag);
    }
}
