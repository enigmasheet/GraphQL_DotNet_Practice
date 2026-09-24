using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Comments;

/// <summary>
/// Node resolver for the Relay global object identification pattern.
/// </summary>
public static class CommentNodeResolver
{
    public static Task<Comment?> GetCommentAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Comments.FirstOrDefaultAsync(c => c.Id == id, ct);
}
