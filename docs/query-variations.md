# Query variations

The same data, asked for in many shapes. Every example runs in Nitro at
<http://localhost:5100/graphql> against the seeded database.

> **This schema uses connections everywhere.** Every list field is a Relay connection
> (`Author.posts`, `BlogPost.comments`, `BlogPost.tags`, `Query.posts`, `Query.authors`,
> `Query.tags`) and `RequirePagingBoundaries` is on, so a paged field **must** get `first` (or `last`).
> Omitting it fails with `HC0082` "Exactly one slicing argument must be defined."
> Scalar fields (`id`, `title`, `totalCount`, …) are selected bare; object/list fields need `{ … }`.

## 1. Minimal vs full selection

Scalars only:

```graphql
{ posts(first: 3) { nodes { id title } } }
```

With relations and paging metadata:

```graphql
{
  posts(first: 3) {
    totalCount
    pageInfo { hasNextPage endCursor }
    nodes {
      id
      title
      author { id name }
      tags(first: 5) { nodes { id name } }
      comments(first: 2) { totalCount nodes { id text author { name } } }
    }
  }
}
```

## 2. `nodes` vs `edges` vs `totalCount` + `pageInfo`

Three ways to read the same page:

```graphql
{ posts(first: 2) { nodes { id title } } }
```

```graphql
{ posts(first: 2) { edges { cursor node { id title } } } }
```

```graphql
{ posts(first: 2) { totalCount pageInfo { hasNextPage hasPreviousPage startCursor endCursor } nodes { id } } }
```

`nodes` is the flattened data; `edges` adds each item's opaque `cursor`; `totalCount` is the size of
the whole (filtered) set, not the page.

## 3. Forward vs backward paging

Forward — start at the top, walk with `endCursor`:

```graphql
query Page($after: String) {
  posts(first: 2, after: $after) { pageInfo { hasNextPage endCursor } nodes { id title } }
}
```

```json
{ "after": null }
```

Backward — take the last page and walk with `startCursor`:

```graphql
query LastPage($before: String) {
  posts(last: 2, before: $before) { pageInfo { hasPreviousPage startCursor } nodes { id title } }
}
```

## 4. Nested paged lists

A connection inside a connection — each list gets its own page size:

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

`Author.posts`, `BlogPost.comments` and `BlogPost.tags` also accept `where` and `order`:

```graphql
{
  posts(first: 1) {
    nodes { title comments(first: 2, order: { createdAt: DESC }) { nodes { text } } }
  }
}
```

## 5. Aliases — two different views in one request

GraphQL requires distinct aliases when the same field is requested with different arguments:

```graphql
{
  published: posts(first: 2, where: { status: { eq: PUBLISHED } }) { nodes { title status } }
  drafts: posts(first: 2, where: { status: { eq: DRAFT } }) { nodes { title status } }
}
```

## 6. Variables vs inline literals

Inline:

```graphql
{ posts(first: 2, where: { status: { eq: PUBLISHED } }) { nodes { title } } }
```

Variables (reusable, and the only sane option with big inputs):

```graphql
query List($first: Int!, $where: BlogPostFilterInput, $order: [BlogPostSortInput!]) {
  posts(first: $first, where: $where, order: $order) { nodes { title status } }
}
```

```json
{ "first": 2, "where": { "status": { "eq": "PUBLISHED" } }, "order": [{ "createdAt": "DESC" }] }
```

**Omitting a variable is not the same as `null`:** `{ "where": { "status": { "eq": null } } }` matches
posts with a *null* status (none), not "all statuses".

## 7. Named operations, and several in one document

```graphql
query FirstPage { posts(first: 2) { nodes { id title } } }

query Authors { authors(first: 3) { nodes { name } } }
```

Nitro lets you pick which named operation to run; clients should send `operationName` when a
document holds more than one.

## 8. Fragments

Named fragment reused across a selection:

```graphql
query WithFragment {
  posts(first: 3) {
    nodes {
      ...PostFields
      author { name }
    }
  }
}

fragment PostFields on BlogPost {
  id
  title
  status
}
```

Inline fragments on the mutation error union (each concrete error exposes different fields):

```graphql
mutation Create($input: CreatePostInput!) {
  createPost(input: $input) {
    blogPost { id title }
    errors {
      __typename
      ... on SlugAlreadyInUseError { message slug }
      ... on AuthorNotFoundError { message authorId }
    }
  }
}
```

## 9. Directives — `@include` / `@skip`

Fetch the comments only when a variable says so:

```graphql
query Conditional($withComments: Boolean!) {
  posts(first: 2) {
    nodes {
      title
      comments(first: 1) @include(if: $withComments) { totalCount }
    }
  }
}
```

```json
{ "withComments": false }
```

`@skip(if: $x)` is the mirror image. Directives change the *selection*, and cost analysis accounts
for them.

## 10. Filtering and sorting variations

Filter operators (`eq`, `neq`, `in`, `contains`, `startsWith`, `gt`, `lt`, `and`/`or`):

```graphql
{ posts(first: 5, where: { title: { contains: "graphql" } }) { nodes { title } } }
```

```graphql
{
  posts(first: 5, where: { or: [ { title: { contains: "graphql" } }, { body: { contains: "sql" } } ] }) {
    nodes { title }
  }
}
```

Multiple sort keys, applied in order:

```graphql
{ posts(first: 5, order: [ { status: ASC }, { createdAt: DESC } ]) { nodes { status createdAt title } } }
```

Sort without a `first` is fine for a plain connection, but this schema requires a boundary — always
pass `first`.

## 11. Introspection and `__typename`

```graphql
{ __typename }
```

```graphql
{ __schema { queryType { name } mutationType { name } subscriptionType { name } } }
```

```graphql
{ __type(name: "BlogPost") { fields { name type { kind name ofType { name } } } } }
```

`__typename` is handy on unions — that is how you learn which error you got:

```graphql
mutation { createAuthor(input: { name: "x" }) { author { __typename id } } }
```

## 12. The same operation in three forms

**Raw GraphQL (Nitro):**

```graphql
query GetPosts($first: Int!) { posts(first: $first) { totalCount nodes { id title } } }
```

**Strawberry Shake document** (`src/GraphQLPractice.Client/GraphQL/GetPosts.graphql`) — same text,
compiled at build time into typed C#.

**Generated component** (`Pages/Authors.razor`):

```razor
<UseGetAuthors>
    <ChildContent>@foreach (var a in context.Authors!.Nodes!) { <p>@a.Name</p> }</ChildContent>
    <LoadingContent><p>Loading…</p></LoadingContent>
    <ErrorContent>@context.First().Message</ErrorContent>
</UseGetAuthors>
```

**Imperative client** (`Pages/Posts.razor`):

```csharp
var result = await Client.GetPosts.ExecuteAsync(first, after, where, order);
if (result.IsErrorResult()) { /* result.Errors */ }
var connection = result.Data?.Posts;   // connection.Nodes, connection.TotalCount, connection.PageInfo
```

Server and client speak exactly the same GraphQL — the difference is only who writes the transport
plumbing.

## See also

- Cost and limits: [graphql-tour.md §10](graphql-tour.md#10-debugging) (`HC0047`, `HC0082`, `GraphQL-Cost`).
- Fetching any object by a global id: [graphql-tour.md §15](graphql-tour.md#15-global-object-identification-relay-node).
