using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Authors;

/// <summary>
/// Node resolver for the Relay global object identification pattern. Kept out of
/// <see cref="AuthorQueries"/> so it is not also exposed as a Query field.
/// </summary>
public static class AuthorNodeResolver
{
    public static Task<Author?> GetAuthorAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Authors.FirstOrDefaultAsync(a => a.Id == id, ct);
}
