using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Authors;

[MutationType]
public static partial class AuthorMutations
{
    public static async Task<Author> CreateAuthorAsync(
        string name,
        string? bio,
        AppDbContext db,
        CancellationToken ct
    )
    {
        var author = new Author
        {
            Name = name,
            Bio = bio,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Authors.Add(author);
        await db.SaveChangesAsync(ct);

        return author;
    }
}
