using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.GraphQL.DataLoaders;

internal static class BlogPostDataLoaders
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

    // Group loader: many posts per author. Missing keys resolve to an empty array.
    [DataLoader]
    public static async Task<ILookup<int, BlogPost>> GetPostsByAuthorIdAsync(
        IReadOnlyList<int> authorIds,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var posts = await db
            .BlogPosts.Where(p => authorIds.Contains(p.AuthorId))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        return posts.ToLookup(p => p.AuthorId);
    }
}
