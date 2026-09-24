using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Posts;

internal static class PostDataLoaders
{
    // Batch loader: one post per key.
    [DataLoader]
    public static async Task<IReadOnlyDictionary<int, BlogPost>> GetPostByIdAsync(
        IReadOnlyList<int> ids,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.BlogPosts.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }
}
