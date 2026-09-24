using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Errors;
using GraphQLPractice.Api.Models;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Comments;

[MutationType]
public static partial class CommentMutations
{
    [Error(typeof(AuthorNotFoundException))]
    [Error(typeof(PostNotFoundException))]
    public static async Task<Comment> AddCommentAsync(
        int postId,
        int authorId,
        string text,
        AppDbContext db,
        ITopicEventSender sender,
        CancellationToken ct
    )
    {
        _ = await db.BlogPosts.FindAsync([postId], ct) ?? throw new PostNotFoundException(postId);

        _ =
            await db.Authors.FindAsync([authorId], ct)
            ?? throw new AuthorNotFoundException(authorId);

        var comment = new Comment
        {
            Text = text,
            BlogPostId = postId,
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        // Dynamic topic: subscribers filtered by postId receive only their post's comments.
        await sender.SendAsync($"OnCommentAdded_{postId}", comment, ct);

        return comment;
    }
}
