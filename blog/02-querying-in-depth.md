# Querying in depth: shape, variables, and directives

> Part 2 of the GraphQL .NET Practice study series

In [Part 1](01-why-graphql-and-the-schema.md) you sent your first query and saw that GraphQL returns JSON shaped exactly like your selection set. That was only the first move. In this post you'll learn how the client keeps control of the response: asking for the same field under different arguments with aliases, packing several operations into one document, moving inputs into variables, walking nested relationships, reusing selections with fragments, and toggling fields with directives. Keep the API running at <http://localhost:5100/graphql> and the console visible — every example below runs against the seeded data.

## What you'll learn

- How minimal and full selection sets produce different response shapes
- Aliases, named operations, and multiple operations per document
- Variables vs inline literals, and why omitting a variable is not the same as `null`
- How nested fields resolve through DataLoaders and paged connections
- Fragments and the `@include` / `@skip` directives
- `__typename` and introspection as your schema-discovery tools

## Fields are shaped by the client

The server exposes one schema; the client decides how much of it to read. Ask for scalars only:

```graphql
{ posts(first: 3) { nodes { id title } } }
```

Or ask for relations and paging metadata in the same request:

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

Both hit the same `posts` field. The second one is not "a bigger endpoint" — it is a bigger selection, and Hot Chocolate only resolves the fields you selected.

Aliases let you request the same field twice with different arguments in one response:

```graphql
{
  published: posts(first: 2, where: { status: { eq: PUBLISHED } }) { nodes { title status } }
  drafts: posts(first: 2, where: { status: { eq: DRAFT } }) { nodes { title status } }
}
```

GraphQL requires distinct aliases whenever the same field appears with different arguments. The keys in the JSON are `published` and `drafts`, not `posts`.

## Named operations and multiple operations per document

A document can hold more than one operation, so give them names:

```graphql
query FirstPage { posts(first: 2) { nodes { id title } } }

query Authors { authors(first: 3) { nodes { name } } }
```

Nitro lets you pick which named operation to run. Real clients send `operationName` with the request when a document contains several operations, so the server knows which one to execute.

## Variables vs inline literals

Inline arguments are fine for one-off exploration:

```graphql
{ posts(first: 2, where: { status: { eq: PUBLISHED } }) { nodes { title } } }
```

For anything reusable, move inputs into variables. This is also the only sane option once an input object grows:

```graphql
query List($first: Int!, $where: BlogPostFilterInput, $order: [BlogPostSortInput!]) {
  posts(first: $first, where: $where, order: $order) { nodes { title status } }
}
```

```json
{ "first": 2, "where": { "status": { "eq": "PUBLISHED" } }, "order": [{ "createdAt": "DESC" }] }
```

Two traps are worth memorizing:

- **Omitting a variable is not the same as sending `null`.** `{ "where": { "status": { "eq": null } } }` matches posts whose status *is null* (there are none), not "all statuses". To ignore a filter, omit it entirely.
- **Watch the JSON type.** `postById(id: Int!)` needs the raw key `2`, while `node(id: ID!)` takes the opaque global id *string* `"QmxvZ1Bvc3Q6Mg=="`. GraphQL `ID` is a string scalar; the difference is the encoding, not the JSON type.

## Nested fields and resolvers

Nested objects are resolved by their own resolvers, not by one giant query:

```graphql
{
  posts(first: 3) {
    nodes {
      title
      author { name }
      tags(first: 5) { nodes { name } }
      comments(first: 2) {
        nodes {
          text
          author { name }
          post { title }
        }
      }
    }
  }
}
```

- `BlogPost.author`, `Comment.author` and `Comment.post` are served by the `[ObjectType<T>]` node classes through **DataLoaders**, so N parents collapse into one batched lookup.
- `Author.posts`, `BlogPost.comments` and `BlogPost.tags` are paged connections resolved straight from EF Core with `IQueryable` (see Post 3).
- The raw EF navigation properties are hidden with `[GraphQLIgnore]`, so there is exactly one way to reach each field.

## Fragments and directives

A named fragment reuses a selection across the document:

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

Directives change the selection at execution time. `@include` adds a field only when the variable is true:

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

`@skip(if: $x)` is the mirror image. Directives alter the *selection*, and cost analysis accounts for them.

## __typename and introspection

`__typename` returns the concrete type name and is how you learn which member of a union you received:

```graphql
{ __typename }
```

```graphql
mutation { createAuthor(input: { name: "x" }) { author { __typename id } } }
```

Introspection is the same mechanism Nitro and the Strawberry Shake client use to know your schema:

```graphql
{ __schema { queryType { name } mutationType { name } subscriptionType { name } } }
```

```graphql
{ __type(name: "BlogPost") { fields { name type { kind name ofType { name } } } } }
```

## Try it yourself

1. Run the minimal and full selections back to back. Compare the response shape, then watch the console and compare the SQL.
2. Run the alias query for `published` and `drafts`; confirm the response keys are the aliases.
3. Run `List` with `where` omitted, then with `{ "where": { "status": { "eq": null } } }`. One returns data, one returns nothing.
4. Run the nested query and find the single batched author query (`WHERE a."Id" = ANY (@ids)`) in the console.
5. Run `WithFragment`, then flip `withComments` to `true` and re-run `Conditional`.
6. Introspect `__type(name: "BlogPost")` and confirm `comments` and `tags` are connection types.

```graphql
query List($first: Int!, $where: BlogPostFilterInput, $order: [BlogPostSortInput!]) {
  posts(first: $first, where: $where, order: $order) { nodes { title status } }
}
```

```json
{ "first": 5, "where": { "status": { "eq": "PUBLISHED" } }, "order": [{ "createdAt": "DESC" }] }
```

## Key takeaways

- The selection set is the API contract per request: you ask for exactly what you need.
- Aliases let one request return several views of the same field.
- Variables keep inputs out of the query text; omitting a variable is not the same as `null`.
- Nested objects resolve through DataLoaders, while nested lists are paged connections.
- Fragments and directives compose selections; `__typename` and introspection make the schema discoverable.

## Where to go next

- Back to [Why GraphQL, and how to read the schema](01-why-graphql-and-the-schema.md).
- Continue with [Paging, filtering, and sorting](03-paging-filtering-sorting.md).
- Cookbook of the same query in many shapes: [../docs/query-variations.md](../docs/query-variations.md).
- Full feature tour: [../docs/graphql-tour.md](../docs/graphql-tour.md).
- Project overview: [../README.md](../README.md).
