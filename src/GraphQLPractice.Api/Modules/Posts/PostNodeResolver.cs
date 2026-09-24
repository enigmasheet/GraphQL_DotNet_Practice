using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Posts;

/// <summary>
/// Node resolver for the Relay global object identification pattern. Kept out of
/// <see cref="PostQueries"/> so it is not also exposed as a Query field.
/// </summary>
public static class PostNodeResolver
{
    public static Task<BlogPost?> GetPostAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
}
