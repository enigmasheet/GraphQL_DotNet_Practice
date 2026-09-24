using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Authors.AnyAsync(ct))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var ada = new Author
        {
            Name = "Ada Lovelace",
            Bio = "Wrote the first algorithm intended for a machine.",
            CreatedAt = now.AddDays(-30),
        };
        var alan = new Author
        {
            Name = "Alan Turing",
            Bio = "Father of theoretical computer science.",
            CreatedAt = now.AddDays(-25),
        };
        var grace = new Author
        {
            Name = "Grace Hopper",
            Bio = "Pioneer of compilers and COBOL.",
            CreatedAt = now.AddDays(-20),
        };

        var graphql = new Tag { Name = "graphql" };
        var dotnet = new Tag { Name = "dotnet" };
        var databases = new Tag { Name = "databases" };

        var posts = new[]
        {
            new BlogPost
            {
                Title = "Why GraphQL Exists",
                Slug = "why-graphql-exists",
                Body =
                    "REST returns fixed shapes; GraphQL lets the client ask for exactly what it needs.",
                Status = PostStatus.Published,
                CreatedAt = now.AddDays(-10),
                PublishedAt = now.AddDays(-9),
                Author = ada,
                Tags = [graphql],
            },
            new BlogPost
            {
                Title = "Solving the N+1 Problem",
                Slug = "solving-the-n-plus-one-problem",
                Body = "DataLoaders batch and cache lookups within a single GraphQL request.",
                Status = PostStatus.Published,
                CreatedAt = now.AddDays(-8),
                PublishedAt = now.AddDays(-7),
                Author = ada,
                Tags = [graphql, dotnet],
            },
            new BlogPost
            {
                Title = "Postgres LISTEN/NOTIFY for Subscriptions",
                Slug = "postgres-listen-notify-for-subscriptions",
                Body =
                    "A database-backed pub/sub is a pragmatic default when you already run Postgres.",
                Status = PostStatus.Published,
                CreatedAt = now.AddDays(-6),
                PublishedAt = now.AddDays(-5),
                Author = alan,
                Tags = [databases, graphql],
            },
            new BlogPost
            {
                Title = "Draft: Pagination Patterns",
                Slug = "draft-pagination-patterns",
                Body = "Offset vs cursor pagination and why Relay chose cursors.",
                Status = PostStatus.Draft,
                CreatedAt = now.AddDays(-3),
                Author = grace,
                Tags = [graphql],
            },
            new BlogPost
            {
                Title = "EF Core Projections with Hot Chocolate",
                Slug = "ef-core-projections-with-hot-chocolate",
                Body =
                    "Projections push field selection down into SQL so you only read the columns you asked for.",
                Status = PostStatus.Published,
                CreatedAt = now.AddDays(-2),
                PublishedAt = now.AddDays(-1),
                Author = grace,
                Tags = [dotnet, databases],
            },
        };

        db.Authors.AddRange(ada, alan, grace);
        db.Tags.AddRange(graphql, dotnet, databases);
        db.BlogPosts.AddRange(posts);

        await db.SaveChangesAsync(ct);

        var published = posts.Where(p => p.Status == PostStatus.Published).ToArray();

        db.Comments.AddRange(
            new Comment
            {
                Text = "This finally made the client-driven schema click.",
                CreatedAt = now.AddDays(-8),
                Author = alan,
                BlogPost = published[0],
            },
            new Comment
            {
                Text = "Do you recommend DataLoader for every relationship?",
                CreatedAt = now.AddDays(-7),
                Author = grace,
                BlogPost = published[1],
            },
            new Comment
            {
                Text = "Batching solved our worst query in production.",
                CreatedAt = now.AddDays(-6),
                Author = ada,
                BlogPost = published[1],
            },
            new Comment
            {
                Text = "Curious how this compares to Redis pub/sub.",
                CreatedAt = now.AddDays(-4),
                Author = grace,
                BlogPost = published[2],
            }
        );

        await db.SaveChangesAsync(ct);
    }
}
