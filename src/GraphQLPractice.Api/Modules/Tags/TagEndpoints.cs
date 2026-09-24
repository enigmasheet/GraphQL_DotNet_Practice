using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Tags;

internal static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tags").WithTags("Tags");

        group
            .MapGet(
                "/",
                async (IDbContextFactory<AppDbContext> factory, CancellationToken ct) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var tags = await db
                        .Tags.OrderBy(t => t.Name)
                        .Select(t => new TagDto(t.Id, t.Name))
                        .ToListAsync(ct);

                    return TypedResults.Ok(tags);
                }
            )
            .WithName("ListTags")
            .WithSummary("List all tags");

        group
            .MapGet(
                "/{id:int}",
                async Task<Results<Ok<TagDto>, NotFound>> (
                    int id,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    var tag = await db.Tags.FindAsync([id], ct);
                    return tag is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(new TagDto(tag.Id, tag.Name));
                }
            )
            .WithName("GetTag")
            .WithSummary("Get a single tag by id");

        group
            .MapPost(
                "/",
                async Task<Results<Created<TagDto>, Conflict<string>>> (
                    CreateTagRequest request,
                    IDbContextFactory<AppDbContext> factory,
                    CancellationToken ct
                ) =>
                {
                    await using var db = await factory.CreateDbContextAsync(ct);

                    if (await db.Tags.AnyAsync(t => t.Name == request.Name, ct))
                    {
                        return TypedResults.Conflict(
                            $"A tag named '{request.Name}' already exists."
                        );
                    }

                    var tag = new Tag { Name = request.Name };
                    db.Tags.Add(tag);
                    await db.SaveChangesAsync(ct);

                    return TypedResults.Created(
                        $"/api/tags/{tag.Id}",
                        new TagDto(tag.Id, tag.Name)
                    );
                }
            )
            .WithName("CreateTag")
            .WithSummary("Create a tag");

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

                    var tag = await db.Tags.FindAsync([id], ct);
                    if (tag is null)
                    {
                        return TypedResults.NotFound();
                    }

                    db.Tags.Remove(tag);
                    await db.SaveChangesAsync(ct);

                    return TypedResults.NoContent();
                }
            )
            .WithName("DeleteTag")
            .WithSummary("Delete a tag");

        return endpoints;
    }
}

public sealed record TagDto(int Id, string Name);

public sealed record CreateTagRequest(string Name);
