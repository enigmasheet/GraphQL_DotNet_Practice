using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Errors;
using GraphQLPractice.Api.Models;
using GraphQLPractice.Api.Utilities;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Posts;

[MutationType]
public static partial class PostMutations
{
    [Error(typeof(AuthorNotFoundException))]
    [Error(typeof(SlugAlreadyInUseException))]
    public static async Task<BlogPost> CreatePostAsync(
        string title,
        string body,
        [ID(nameof(Author))] int authorId,
        PostStatus status,
        [ID(nameof(Tag))] int[]? tagIds,
        AppDbContext db,
        ITopicEventSender sender,
        CancellationToken ct
    )
    {
        _ =
            await db.Authors.FindAsync([authorId], ct)
            ?? throw new AuthorNotFoundException(authorId);

        var slug = Slug.From(title);

        if (await db.BlogPosts.AnyAsync(p => p.Slug == slug, ct))
        {
            throw new SlugAlreadyInUseException(slug);
        }

        var post = new BlogPost
        {
            Title = title,
            Slug = slug,
            Body = body,
            AuthorId = authorId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = status == PostStatus.Published ? DateTimeOffset.UtcNow : null,
        };

        if (tagIds is { Length: > 0 })
        {
            post.Tags = await db.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(ct);
        }

        db.BlogPosts.Add(post);
        await db.SaveChangesAsync(ct);

        if (post.Status == PostStatus.Published)
        {
            await sender.SendAsync(nameof(PostSubscription.OnPostPublished), post, ct);
        }

        return post;
    }

    [Error(typeof(PostNotFoundException))]
    [Error(typeof(SlugAlreadyInUseException))]
    public static async Task<BlogPost> UpdatePostAsync(
        [ID(nameof(BlogPost))] int id,
        string? title,
        string? body,
        PostStatus? status,
        AppDbContext db,
        CancellationToken ct
    )
    {
        var post = await db.BlogPosts.FindAsync([id], ct) ?? throw new PostNotFoundException(id);

        if (title is not null)
        {
            var slug = Slug.From(title);

            if (!string.Equals(slug, post.Slug, StringComparison.Ordinal))
            {
                if (await db.BlogPosts.AnyAsync(p => p.Slug == slug, ct))
                {
                    throw new SlugAlreadyInUseException(slug);
                }

                post.Title = title;
                post.Slug = slug;
            }
        }

        if (body is not null)
        {
            post.Body = body;
        }

        if (status is not null)
        {
            post.Status = status.Value;
            post.PublishedAt =
                status.Value == PostStatus.Published
                    ? post.PublishedAt ?? DateTimeOffset.UtcNow
                    : null;
        }

        await db.SaveChangesAsync(ct);

        return post;
    }

    [Error(typeof(PostNotFoundException))]
    public static async Task<BlogPost> DeletePostAsync(
        [ID(nameof(BlogPost))] int id,
        AppDbContext db,
        CancellationToken ct
    )
    {
        var post = await db.BlogPosts.FindAsync([id], ct) ?? throw new PostNotFoundException(id);

        db.BlogPosts.Remove(post);
        await db.SaveChangesAsync(ct);

        return post;
    }
}
