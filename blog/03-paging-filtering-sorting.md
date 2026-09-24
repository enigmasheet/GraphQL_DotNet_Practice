# Paging, filtering, and sorting: connections end to end

> Part 3 of the GraphQL .NET Practice study series

In [Post 2](02-querying-in-depth.md) you learned to shape a response with selections, aliases, and variables. Every list you touched there was a Relay **connection**, and that shape is what makes paging, filtering, and sorting uniform across the whole schema. In this post you'll learn the connection anatomy (`nodes`, `edges`, `totalCount`, `pageInfo`), how forward and backward paging differ, why this server demands a page boundary, how nested lists get their own caps, and how `where` and `order` reach EF Core. Watch the API console at <http://localhost:5100/graphql> — the SQL it prints is half the lesson.

## What you'll learn

- The parts of a Relay connection and what each one is for
- Forward (`first`/`after`) vs backward (`last`/`before`) paging
- Why every list needs a boundary (`RequirePagingBoundaries`, `HC0082`)
- Nested paged lists and their per-field `MaxPageSize` caps (`HC0051`)
- Filtering inputs (`where` with `eq`, `contains`, `or`/`and`/`not`) and sorting (`order`, `IfEmpty`)
- How resolvers use `PagingArguments` and `QueryContext<T>`, and how to read the SQL they produce

## Anatomy of a Relay connection

Every list field returns a connection. Read the same page three ways:

```graphql
{ posts(first: 2) { nodes { id title } } }
```

```graphql
{ posts(first: 2) { edges { cursor node { id title } } } }
```

```graphql
{ posts(first: 2) { totalCount pageInfo { hasNextPage hasPreviousPage startCursor endCursor } nodes { id } } }
```

- `nodes` is the flattened data — usually all you need.
- `edges` adds each item's opaque `cursor`, used for paging.
- `totalCount` is the size of the whole **filtered** set, not the page.
- `pageInfo` carries `hasNextPage`, `hasPreviousPage`, `startCursor`, and `endCursor`.

`nodes` and `edges` are two views of the same items; pick one per request.

## Forward vs backward paging

Forward paging starts at the top and walks with `endCursor`:

```graphql
query Page($after: String) {
  posts(first: 2, after: $after) { pageInfo { hasNextPage endCursor } nodes { id title } }
}
```

```json
{ "after": null }
```

Send `after: null` for the first page, then feed the previous response's `endCursor` back in. Backward paging takes the last page and walks with `startCursor`:

```graphql
query LastPage($before: String) {
  posts(last: 2, before: $before) { pageInfo { hasPreviousPage startCursor } nodes { id title } }
}
```

Use `first`/`after` to scroll a feed; use `last`/`before` when you want the tail (for example, the newest N in a descending list).

## Every connection needs a boundary

`RequirePagingBoundaries` is on for this schema, so a paged field must be given `first` or `last`. Omit it and execution fails with `HC0082`:

```graphql
{ posts { nodes { id } } }
# => "Exactly one slicing argument must be defined." — HC0082
```

Add a boundary and it succeeds:

```graphql
{ posts(first: 10) { nodes { id } } }
```

This is deliberate: an unbounded list is an easy way to accidentally ask a server for a table.

## Nested paged lists and their caps

A connection inside a connection gets its own page size. The root lists and the nested lists have different `MaxPageSize` values:

| Field | MaxPageSize |
|---|---|
| `Query.posts`, `Query.authors`, `Query.tags` | 50 |
| `Author.posts` | 10 |
| `BlogPost.comments` | 50 |
| `BlogPost.tags` | 20 |

Exceed a cap and the field fails with `HC0051`:

```graphql
{ posts(first: 100) { nodes { id } } }
# => "The maximum allowed items per page were exceeded." — HC0051, maxAllowedItems: 50
```

Each nested list sets its own boundary, so this is valid even though the inner `first` values differ from the outer one:

```graphql
{
  posts(first: 2) {
    nodes {
      title
      comments(first: 2) { totalCount nodes { text author { name } } }
      tags(first: 5) { nodes { name } }
    }
  }
}
```

Remember the nested caps when you compose: `tags(first: 25)` under a post fails `HC0051` because that field allows 20.

## Filtering and sorting inputs

Filtering is generated per type (`BlogPostFilterInput`, `AuthorFilterInput`, …). Operators include `eq`, `neq`, `in`, `nin`, `contains`, `startsWith`, `gt`, `lt`, plus the boolean combinators `and`, `or`, and `not`:

```graphql
{ posts(first: 5, where: { title: { contains: "graphql" }, status: { eq: PUBLISHED } }) { nodes { title } } }
```

```graphql
{
  posts(first: 5, where: { or: [ { title: { contains: "graphql" } }, { body: { contains: "sql" } } ] }) {
    nodes { title }
  }
}
```

Sorting takes one input or a list, applied in order:

```graphql
{ posts(first: 5, order: [{ status: ASC }, { createdAt: DESC }]) { nodes { status createdAt title } } }
```

If you pass no `order`, the resolver applies a default. In `PostQueries.cs` the call is `.With(query, sort => sort.IfEmpty(s => s.AddAscending(p => p.Id)))` — `IfEmpty` means "only if the client sent nothing", so results are stably ordered by `Id`. The same pattern guards `authors` and `tags`.

## How the resolvers use PagingArguments and QueryContext

The root query is short because the framework does the plumbing:

```csharp
[UseConnection(IncludeTotalCount = true, MaxPageSize = 50)]
[UseFiltering]
[UseSorting]
public static async Task<PageConnection<BlogPost>> GetPostsAsync(
    PagingArguments pagingArgs,
    QueryContext<BlogPost> query,
    AppDbContext db,
    CancellationToken ct
) =>
    await db
        .BlogPosts.With(query, sort => sort.IfEmpty(s => s.AddAscending(p => p.Id)))
        .ToPageAsync(pagingArgs, ct);
```

- `PagingArguments` carries `first`/`after` (or `last`/`before`) from the request.
- `QueryContext<BlogPost>` carries the client's selection, `where`, and `order` — it **replaces** the older `[UseProjection]` attribute, so never combine the two (the `HC0099` analyzer warns).
- `.With(...)` applies filter/sort/projection; `.ToPageAsync(...)` runs the query and builds the connection.

Nested connections follow the same shape and add `[Parent(requires: nameof(T.Id))]` so the join key is always projected. `Author.posts` filters by `AuthorId`; `BlogPost.comments` by `BlogPostId`; `BlogPost.tags` walks the join table with `t.Posts.Any(p => p.Id == post.Id)`:

```csharp
[UseConnection(IncludeTotalCount = true, MaxPageSize = 10)]
[UseFiltering]
[UseSorting]
public static async Task<PageConnection<BlogPost>> GetPostsAsync(
    [Parent(requires: nameof(Author.Id))] Author author,
    PagingArguments pagingArgs,
    QueryContext<BlogPost> query,
    AppDbContext db,
    CancellationToken ct
) =>
    await db
        .BlogPosts.Where(p => p.AuthorId == author.Id)
        .With(query, sort => sort.IfEmpty(s => s.AddAscending(p => p.Id)))
        .ToPageAsync(pagingArgs, ct);
```

Because `QueryContext` projects from your selection set, EF Core reads only the columns you asked for. Run `{ tags(first: 2) { nodes { name } } }` and compare the console against `{ tags(first: 2) { nodes { id name } } }` — the first selects only `Name`. `Microsoft.EntityFrameworkCore.Database.Command` is set to `Information` in `appsettings.Development.json`, which is why you can see it.

## Try it yourself

1. Run `{ posts(first: 2) { nodes { id title } } }`, copy `endCursor`, then run the forward query with `after` set to it.
2. Run the backward query with `last: 2` and no `before`; inspect `hasPreviousPage`.
3. Run `{ posts { nodes { id } } }` and read the `HC0082` error, then add `first: 10`.
4. Run `{ posts(first: 100) { nodes { id } } }` and confirm `HC0051` reports `maxAllowedItems: 50`.
5. Try `posts(first: 2) { nodes { tags(first: 25) { nodes { name } } } }` and see the nested cap reject it.
6. Add `order: [{ createdAt: DESC }]`, then remove it and watch the default `Id` ordering return.
7. Add the header `GraphQL-Cost: report` and compare `fieldCost` for a flat page vs a nested one.

```graphql
query Filtered($where: BlogPostFilterInput, $order: [BlogPostSortInput!]) {
  posts(first: 5, where: $where, order: $order) {
    totalCount
    pageInfo { hasNextPage endCursor }
    nodes { title status }
  }
}
```

```json
{ "where": { "title": { "contains": "graphql" } }, "order": [{ "createdAt": "DESC" }] }
```

## Key takeaways

- Every list is a Relay connection: `nodes`/`edges` for data, `totalCount` for the filtered size, `pageInfo` for cursors.
- Paging is cursor-based and bidirectional; feed `endCursor` forward or `startCursor` backward.
- `RequirePagingBoundaries` makes `first`/`last` mandatory — omitting one is `HC0082`.
- Nested lists have their own caps (`Author.posts` 10, `BlogPost.comments` 50, `BlogPost.tags` 20); exceeding one is `HC0051`.
- `where` and `order` flow through `QueryContext<T>` into EF Core, and `IfEmpty` supplies the default `Id` ordering.

## Where to go next

- Revisit [Querying in depth](02-querying-in-depth.md) for variables and directives.
- Paging and filtering cookbook: [../docs/query-variations.md](../docs/query-variations.md).
- Cost, `HC0047`, and error codes: [../docs/graphql-tour.md](../docs/graphql-tour.md).
- Project overview and schema: [../README.md](../README.md).
