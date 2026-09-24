using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.GraphQL.DataLoaders;

internal static class CommentDataLoaders
{
    // Group loader: many comments per post.
    [DataLoader]
    public static async Task<ILookup<int, Comment>> GetCommentsByPostIdAsync(
        IReadOnlyList<int> postIds,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var comments = await db
            .Comments.Where(c => postIds.Contains(c.BlogPostId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return comments.ToLookup(c => c.BlogPostId);
    }
}
