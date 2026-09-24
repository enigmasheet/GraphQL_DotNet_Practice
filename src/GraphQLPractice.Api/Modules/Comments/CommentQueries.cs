using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Comments;

[QueryType]
public static partial class CommentQueries
{
    // Node resolver for the Relay global object identification pattern.
    [NodeResolver]
    [GraphQLIgnore]
    public static Task<Comment?> ResolveCommentAsync(
        int id,
        AppDbContext db,
        CancellationToken ct
    ) => db.Comments.FirstOrDefaultAsync(c => c.Id == id, ct);
}
