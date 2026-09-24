using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Comments;

internal static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/comments").WithTags("Comments");

        group
            .MapGet(
                "/",
                async (
                    IDbContextFactory<AppDbContext> factory,
                    int? postId,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var query = db.Comments.AsQueryable();
                    if (postId is not null)
                    {
                        query = query.Where(c => c.BlogPostId == postId);
                    }

                    var comments = await query
                        .OrderBy(c => c.CreatedAt)
                        .Select(c => new CommentDto(
                            c.Id,
                            c.BlogPostId,
                            c.AuthorId,
                            c.Text,
                            c.CreatedAt
                        ))
                        .ToListAsync(ct);

                    return TypedResults.Ok(comments);
                }
            )
            .WithName("ListComments")
            .WithSummary("List comments, optionally filtered by postId");

        group
            .MapGet(
                "/{id:int}",
                async Task<Results<Ok<CommentDto>, NotFound>> (
                    int id,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var comment = await db.Comments.FindAsync([id], ct);
                    return comment is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(
                            new CommentDto(
                                comment.Id,
                                comment.BlogPostId,
                                comment.AuthorId,
                                comment.Text,
                                comment.CreatedAt
                            )
                        );
                }
            )
            .WithName("GetComment")
            .WithSummary("Get a single comment by id");

        group
            .MapPost(
                "/",
                async Task<Results<Created<CommentDto>, BadRequest<string>>> (
                    CreateCommentRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    if (!await db.BlogPosts.AnyAsync(p => p.Id == request.BlogPostId, ct))
                    {
                        return TypedResults.BadRequest($"Post {request.BlogPostId} was not found.");
                    }

                    if (!await db.Authors.AnyAsync(a => a.Id == request.AuthorId, ct))
                    {
                        return TypedResults.BadRequest($"Author {request.AuthorId} was not found.");
                    }

                    var comment = new Comment
                    {
                        BlogPostId = request.BlogPostId,
                        AuthorId = request.AuthorId,
                        Text = request.Text,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };

                    db.Comments.Add(comment);
                    await db.SaveChangesAsync(ct);

                    var dto = new CommentDto(
                        comment.Id,
                        comment.BlogPostId,
                        comment.AuthorId,
                        comment.Text,
                        comment.CreatedAt
                    );
                    return TypedResults.Created($"/api/comments/{comment.Id}", dto);
                }
            )
            .WithName("CreateComment")
            .WithSummary("Add a comment to a post");

        group
            .MapPut(
                "/{id:int}",
                async Task<Results<Ok<CommentDto>, NotFound>> (
                    int id,
                    UpdateCommentRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var comment = await db.Comments.FindAsync([id], ct);
                    if (comment is null)
                    {
                        return TypedResults.NotFound();
                    }

                    comment.Text = request.Text;
                    await db.SaveChangesAsync(ct);

                    return TypedResults.Ok(
                        new CommentDto(
                            comment.Id,
                            comment.BlogPostId,
                            comment.AuthorId,
                            comment.Text,
                            comment.CreatedAt
                        )
                    );
                }
            )
            .WithName("UpdateComment")
            .WithSummary("Update a comment");

        group
            .MapDelete(
                "/{id:int}",
                async Task<Results<NoContent, NotFound>> (
                    int id,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var comment = await db.Comments.FindAsync([id], ct);
                    if (comment is null)
                    {
                        return TypedResults.NotFound();
                    }

                    db.Comments.Remove(comment);
                    await db.SaveChangesAsync(ct);

                    return TypedResults.NoContent();
                }
            )
            .WithName("DeleteComment")
            .WithSummary("Delete a comment");

        return endpoints;
    }
}

public sealed record CommentDto(
    int Id,
    int BlogPostId,
    int AuthorId,
    string Text,
    DateTimeOffset CreatedAt
);

public sealed record CreateCommentRequest(int BlogPostId, int AuthorId, string Text);

public sealed record UpdateCommentRequest(string Text);
