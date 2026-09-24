using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Models;
using HotChocolate.Data;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.GraphQL;

[QueryType]
public static partial class Query
{
    // Paging + filtering + sorting translated straight to SQL over IQueryable.
    // No [UseProjection] here on purpose: nested fields are resolved by the DataLoaders
    // below so you can watch the N+1 problem get solved in the logs.
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 50)]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Author> GetAuthors(AppDbContext db) => db.Authors;

    [UsePaging(IncludeTotalCount = true, MaxPageSize = 50)]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<BlogPost> GetPosts(AppDbContext db) => db.BlogPosts;

    public static Task<Author?> GetAuthorByIdAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.Authors.FirstOrDefaultAsync(a => a.Id == id, ct);

    public static Task<BlogPost?> GetPostByIdAsync(int id, AppDbContext db, CancellationToken ct) =>
        db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);

    // [UseProjection] pushes the selection set into the SQL query: EF Core reads only the
    // columns the client asked for. Compare the generated SQL with the queries above.
    [UseProjection]
    public static IQueryable<Tag> GetTags(AppDbContext db) => db.Tags;
}
