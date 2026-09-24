using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Errors;
using GraphQLPractice.Api.Models;
using GraphQLPractice.Api.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Posts;

internal static class PostEndpoints
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/posts").WithTags("Posts");

        group
            .MapGet(
                "/",
                async (
                    IDbContextFactory<AppDbContext> factory,
                    int? authorId,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var query = db.BlogPosts.AsQueryable();
                    if (authorId is not null)
                    {
                        query = query.Where(p => p.AuthorId == authorId);
                    }

                    var posts = await query
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => new PostDto(
                            p.Id,
                            p.Title,
                            p.Slug,
                            p.Body,
                            p.Status.ToString(),
                            p.AuthorId,
                            p.CreatedAt,
                            p.PublishedAt
                        ))
                        .ToListAsync(ct);

                    return TypedResults.Ok(posts);
                }
            )
            .WithName("ListPosts")
            .WithSummary("List posts, optionally filtered by authorId");

        group
            .MapGet(
                "/{id:int}",
                async Task<Results<Ok<PostDto>, NotFound>> (
                    int id,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var post = await db.BlogPosts.FindAsync([id], ct);
                    return post is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(post));
                }
            )
            .WithName("GetPost")
            .WithSummary("Get a single post by id");

        group
            .MapPost(
                "/",
                async Task<Results<Created<PostDto>, BadRequest<string>, Conflict<string>>> (
                    CreatePostRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    if (!await db.Authors.AnyAsync(a => a.Id == request.AuthorId, ct))
                    {
                        return TypedResults.BadRequest($"Author {request.AuthorId} was not found.");
                    }

                    var slug = Slug.From(request.Title);
                    if (await db.BlogPosts.AnyAsync(p => p.Slug == slug, ct))
                    {
                        return TypedResults.Conflict($"A post with slug '{slug}' already exists.");
                    }

                    var post = new BlogPost
                    {
                        Title = request.Title,
                        Slug = slug,
                        Body = request.Body,
                        AuthorId = request.AuthorId,
                        Status = request.Status,
                        CreatedAt = DateTimeOffset.UtcNow,
                        PublishedAt =
                            request.Status == PostStatus.Published ? DateTimeOffset.UtcNow : null,
                    };

                    if (request.TagIds is { Length: > 0 })
                    {
                        post.Tags = await db
                            .Tags.Where(t => request.TagIds.Contains(t.Id))
                            .ToListAsync(ct);
                    }

                    db.BlogPosts.Add(post);
                    await db.SaveChangesAsync(ct);

                    return TypedResults.Created($"/api/posts/{post.Id}", ToDto(post));
                }
            )
            .WithName("CreatePost")
            .WithSummary("Create a post");

        group
            .MapPut(
                "/{id:int}",
                async Task<Results<Ok<PostDto>, NotFound, Conflict<string>>> (
                    int id,
                    UpdatePostRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var post = await db.BlogPosts.FindAsync([id], ct);
                    if (post is null)
                    {
                        return TypedResults.NotFound();
                    }

                    if (request.Title is not null)
                    {
                        var slug = Slug.From(request.Title);
                        if (
                            !string.Equals(slug, post.Slug, StringComparison.Ordinal)
                            && await db.BlogPosts.AnyAsync(p => p.Slug == slug, ct)
                        )
                        {
                            return TypedResults.Conflict(
                                $"A post with slug '{slug}' already exists."
                            );
                        }

                        post.Title = request.Title;
                        post.Slug = slug;
                    }

                    if (request.Body is not null)
                    {
                        post.Body = request.Body;
                    }

                    if (request.Status is not null)
                    {
                        post.Status = request.Status.Value;
                        post.PublishedAt =
                            request.Status.Value == PostStatus.Published
                                ? post.PublishedAt ?? DateTimeOffset.UtcNow
                                : null;
                    }

                    await db.SaveChangesAsync(ct);

                    return TypedResults.Ok(ToDto(post));
                }
            )
            .WithName("UpdatePost")
            .WithSummary("Update a post");

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

                    var post = await db.BlogPosts.FindAsync([id], ct);
                    if (post is null)
                    {
                        return TypedResults.NotFound();
                    }

                    db.BlogPosts.Remove(post);
                    await db.SaveChangesAsync(ct);

                    return TypedResults.NoContent();
                }
            )
            .WithName("DeletePost")
            .WithSummary("Delete a post");

        return endpoints;
    }

    private static PostDto ToDto(BlogPost post) =>
        new(
            post.Id,
            post.Title,
            post.Slug,
            post.Body,
            post.Status.ToString(),
            post.AuthorId,
            post.CreatedAt,
            post.PublishedAt
        );
}

public sealed record PostDto(
    int Id,
    string Title,
    string Slug,
    string Body,
    string Status,
    int AuthorId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt
);

public sealed record CreatePostRequest(
    string Title,
    string Body,
    int AuthorId,
    PostStatus Status,
    int[]? TagIds
);

public sealed record UpdatePostRequest(string? Title, string? Body, PostStatus? Status);
