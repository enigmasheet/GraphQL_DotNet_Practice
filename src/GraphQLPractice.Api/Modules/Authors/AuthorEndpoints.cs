using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Authors;

internal static class AuthorEndpoints
{
    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/authors").WithTags("Authors");

        group
            .MapGet(
                "/",
                async (IDbContextFactory<AppDbContext> factory, CancellationToken ct) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var authors = await db
                        .Authors.OrderBy(a => a.Name)
                        .Select(a => new AuthorDto(a.Id, a.Name, a.Bio, a.CreatedAt))
                        .ToListAsync(ct);

                    return TypedResults.Ok(authors);
                }
            )
            .WithName("ListAuthors")
            .WithSummary("List all authors");

        group
            .MapGet(
                "/{id:int}",
                async Task<Results<Ok<AuthorDto>, NotFound>> (
                    int id,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var author = await db.Authors.FindAsync([id], ct);
                    return author is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(
                            new AuthorDto(author.Id, author.Name, author.Bio, author.CreatedAt)
                        );
                }
            )
            .WithName("GetAuthor")
            .WithSummary("Get a single author by id");

        group
            .MapPost(
                "/",
                async Task<Created<AuthorDto>> (
                    CreateAuthorRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var author = new Author
                    {
                        Name = request.Name,
                        Bio = request.Bio,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };

                    db.Authors.Add(author);
                    await db.SaveChangesAsync(ct);

                    var dto = new AuthorDto(author.Id, author.Name, author.Bio, author.CreatedAt);
                    return TypedResults.Created($"/api/authors/{author.Id}", dto);
                }
            )
            .WithName("CreateAuthor")
            .WithSummary("Create an author");

        group
            .MapPut(
                "/{id:int}",
                async Task<Results<Ok<AuthorDto>, NotFound>> (
                    int id,
                    UpdateAuthorRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var author = await db.Authors.FindAsync([id], ct);
                    if (author is null)
                    {
                        return TypedResults.NotFound();
                    }

                    author.Name = request.Name;
                    author.Bio = request.Bio;
                    await db.SaveChangesAsync(ct);

                    return TypedResults.Ok(
                        new AuthorDto(author.Id, author.Name, author.Bio, author.CreatedAt)
                    );
                }
            )
            .WithName("UpdateAuthor")
            .WithSummary("Update an author");

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

                    var author = await db.Authors.FindAsync([id], ct);
                    if (author is null)
                    {
                        return TypedResults.NotFound();
                    }

                    db.Authors.Remove(author);
                    await db.SaveChangesAsync(ct);

                    return TypedResults.NoContent();
                }
            )
            .WithName("DeleteAuthor")
            .WithSummary("Delete an author");

        return endpoints;
    }
}

public sealed record AuthorDto(int Id, string Name, string? Bio, DateTimeOffset CreatedAt);

public sealed record CreateAuthorRequest(string Name, string? Bio);

public sealed record UpdateAuthorRequest(string Name, string? Bio);
