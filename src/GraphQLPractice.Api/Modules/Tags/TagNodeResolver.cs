using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Tags;

/// <summary>
/// Node resolver for the Relay global object identification pattern. Kept out of
/// <see cref="TagQueries"/> so it is not also exposed as a Query field.
/// </summary>
public static class TagNodeResolver
{
    public static Task<Tag?> GetTagAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
}
