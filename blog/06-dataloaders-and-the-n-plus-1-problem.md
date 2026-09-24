# DataLoaders and the N+1 problem

> Part 6 of the GraphQL .NET Practice study series

GraphQL lets a client ask for a nested graph in one request, but the server still talks to a relational database one query at a time. A list of posts that each need their author is the classic trap: fetch N posts, then fetch one author per post. That is the N+1 problem. Hot Chocolate solves it with DataLoaders, which batch and cache lookups per request. Here you will see exactly where the extra queries come from, how this repo's two loaders collapse them into one, and how to watch it happen in the SQL log.

## What you'll learn

- Why a list of posts with `author` produces N+1 queries
- How a `[DataLoader]` batch method works and how the loader caches per request
- The `AuthorById` and `PostById` loaders and their generated interfaces
- Why EF navigation properties are hidden with `[GraphQLIgnore]`
- How to read the batched `WHERE a."Id" = ANY (@ids)` in the SQL log
- Why nested connections don't need a DataLoader

## The N+1 problem, concretely

Ask for five posts and each one's author:

```graphql
{
  posts(first: 5) {
    nodes {
      title
      author { name }
    }
  }
}
```

The naive execution is two steps: one query for the page of posts, then one more query for each post's author. Five posts means 1 + 5 = 6 round trips; fifty posts means 51. The shape of the request drives the number of queries, which is exactly the trap. The `author` resolver has no idea a sibling post also needs an author, unless something collects the keys first.

## How a DataLoader batches and caches

A DataLoader is a per-request batching primitive. Instead of resolving one key at a time, it collects the keys requested during a single execution tick and calls your batch function once with all of them.

```csharp
internal static class AuthorDataLoaders
{
    // Batch loader: one query for all requested author ids (deduplicated).
    [DataLoader]
    public static async Task<IReadOnlyDictionary<int, Author>> GetAuthorByIdAsync(
        IReadOnlyList<int> ids,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.Authors.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
    }
}
```

The `[DataLoader]` attribute makes Hot Chocolate source-generate an interface — `IAuthorByIdDataLoader`, named after the method minus `Async` — plus a concrete loader. The method receives a list of keys and returns a dictionary keyed by the same ids, so `ids.Contains(a.Id)` becomes one `IN`/`ANY` query.

Two properties matter:

- **Batching** — many keys become one database round trip.
- **Caching** — a key already loaded in this request is served from memory, so parents that share an author cost nothing extra.

The cache is scoped to the request, which is what you want with EF `DbContext` lifetimes. Registration is source-generated: `Modules/ModuleInfo.cs` declares `[assembly: Module("Types")]` and `[assembly: DataLoaderModule("DataLoaders")]`, which produces the `AddTypes()` and `AddDataLoaders()` calls in `Program.cs`.

## Two loaders, one resolution path

The domain has exactly two batch loaders for the to-one relations:

| Loader | Generated interface | Resolves |
|---|---|---|
| `AuthorById` | `IAuthorByIdDataLoader` | `BlogPost.author`, `Comment.author` |
| `PostById` | `IPostByIdDataLoader` | `Comment.post` |

`PostDataLoaders.GetPostByIdAsync` is the same pattern keyed by post id: a `[DataLoader]` method that takes `IReadOnlyList<int> ids` and returns an `IReadOnlyDictionary<int, BlogPost>`.

The EF navigation properties themselves are hidden. In `Models/BlogPost.cs`:

```csharp
public int AuthorId { get; set; }

// Navigations are resolved by BlogPostNode via DataLoaders.
[GraphQLIgnore]
public Author Author { get; set; } = default!;
```

`[GraphQLIgnore]` removes `Author` from the schema. Without it, Hot Chocolate would expose a second `author` field that reads the navigation directly — an unbatched path that silently reintroduces N+1. Hiding it guarantees there is exactly one way to resolve an author, and that way is the loader.

## Walking the node resolvers

The `[ObjectType<T>]` classes attach the fields. `PostNode.GetAuthorAsync` takes the parent post and the loader:

```csharp
public static async Task<Author> GetAuthorAsync(
    [Parent(requires: nameof(BlogPost.AuthorId))] BlogPost post,
    IAuthorByIdDataLoader authorById,
    CancellationToken ct
) => await authorById.LoadRequiredAsync(post.AuthorId, ct);
```

`[Parent(requires: nameof(BlogPost.AuthorId))]` tells Hot Chocolate to project `AuthorId` into the parent, so the resolver has the key without loading the navigation. `LoadRequiredAsync` queues the key and returns the author; if none exists it throws (use `LoadAsync` when null is acceptable).

`CommentNode` does the same for both of its parents:

```csharp
public static async Task<Author> GetAuthorAsync(
    [Parent(requires: nameof(Comment.AuthorId))] Comment comment,
    IAuthorByIdDataLoader authorById,
    CancellationToken ct
) => await authorById.LoadRequiredAsync(comment.AuthorId, ct);

public static async Task<BlogPost> GetPostAsync(
    [Parent(requires: nameof(Comment.BlogPostId))] Comment comment,
    IPostByIdDataLoader postById,
    CancellationToken ct
) => await postById.LoadRequiredAsync(comment.BlogPostId, ct);
```

`Comment.post` reuses the same `PostById` loader as the query layer, so several comments that point at one post are served from the cache.

## Watch it in the SQL log

`appsettings.Development.json` sets `Microsoft.EntityFrameworkCore.Database.Command` to `Information`, so every statement is printed. Run the first query and watch the console:

```sql
SELECT p."Id", p."Title", p."AuthorId", ...
FROM "BlogPosts" AS p
ORDER BY p."Id"
LIMIT @p

SELECT a."Id", a."Name", a."Bio", a."CreatedAt"
FROM "Authors" AS a
WHERE a."Id" = ANY (@ids)
```

One query for the posts, one batched query for the authors — regardless of how many posts you asked for. Remove `author { name }` and run again: the author query disappears entirely. That is the whole N+1 fix, visible in two log lines.

## Why nested connections don't need a loader

`BlogPost.comments`, `BlogPost.tags` and `Author.posts` are paged connections, not single lookups, so they resolve straight from EF with `IQueryable`:

```csharp
public static async Task<PageConnection<Comment>> GetCommentsAsync(
    [Parent(requires: nameof(BlogPost.Id))] BlogPost post,
    PagingArguments pagingArgs,
    QueryContext<Comment> query,
    AppDbContext db,
    CancellationToken ct
) =>
    await db.Comments.Where(c => c.BlogPostId == post.Id)
        .With(query, sort => sort.IfEmpty(s => s.AddAscending(c => c.Id)))
        .ToPageAsync(pagingArgs, ct);
```

Each nested list is already a filter on the parent key, and `ToPageAsync` runs one query per list per parent. There is no batch of keys to collect, and `QueryContext` already projects only the selected columns. Nested caps apply — `Author.posts` 10, `BlogPost.comments` 50, `BlogPost.tags` 20 — and exceeding one is `HC0051`.

## Try it yourself

1. Start the API and open <http://localhost:5100/graphql>:
   ```powershell
   dotnet run --project src\GraphQLPractice.Api
   ```
2. Run the N+1 query and read the console:
   ```graphql
   { posts(first: 5) { nodes { title author { name } } } }
   ```
   Confirm one posts query plus one `WHERE a."Id" = ANY (@ids)` query.
3. Remove `author { name }`, run again, and confirm the author query is gone.
4. Load a post's comments with both parents and watch `PostById` batch:
   ```graphql
   { posts(first: 1) { nodes { comments(first: 10) { nodes { text author { name } post { title } } } } } }
   ```
5. Raise `first` to 20 on the posts query and confirm the author query stays a single batched statement.

## Key takeaways

- N+1: a list of parents that each resolve a to-one relation produces one query per parent unless the keys are batched.
- A `[DataLoader]` batch method takes a list of keys and returns a dictionary; Hot Chocolate source-generates the interface and loader.
- Loaders batch and cache per request, so repeated keys cost nothing extra.
- `[GraphQLIgnore]` on EF navigations removes the unbatched path, leaving exactly one way to resolve each to-one relation.
- `[Parent(requires: ...)]` projects the foreign key so the resolver never touches the navigation.
- Paged connections use `IQueryable` + `ToPageAsync`; they are already per-parent and don't need a loader.

## Where to go next

- Global object identification: [07-global-object-identification.md](07-global-object-identification.md)
- Nested connections and caps: [03-paging-filtering-sorting.md](03-paging-filtering-sorting.md)
- Nested resolvers in the tour: [../docs/graphql-tour.md](../docs/graphql-tour.md)
- Server overview: [../src/GraphQLPractice.Api/README.md](../src/GraphQLPractice.Api/README.md)
- Project README: [../README.md](../README.md)
