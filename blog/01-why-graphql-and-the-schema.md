# Why GraphQL, and how to read the schema

> Part 1 of the GraphQL .NET Practice study series

A REST API makes you guess. You read the docs, call an endpoint, and discover the response carries fields you did not need and is missing the one you did. GraphQL flips that: the server publishes a typed contract — the schema — and the client asks for exactly the shape it wants. This post explains why that contract exists, what this repo's schema looks like, and how to interrogate it so you never have to guess again.

## What you'll learn

- The tiny blog domain that drives every example: Author → BlogPost → Comment, plus Tags
- The exact stack and versions this repo pins
- What a GraphQL schema is and why it is the source of truth
- Why every list here is a connection, and what `RequirePagingBoundaries` forces you to do
- How global object identification turns every `id` into an opaque `ID!` string
- Where each feature lives in the repository

## The domain and the stack

The domain is deliberately small: an **Author** writes **BlogPosts**, and each post has **Comments**. **Tags** attach to posts many-to-many, and a post's **PostStatus** is either `PUBLISHED` or `DRAFT`. That is enough to exercise nested resolvers, paging, filtering, sorting, mutations, and subscriptions without drowning in business rules.

| Layer | Technology |
|---|---|
| GraphQL server | Hot Chocolate **16.6.6** (code-first) |
| Architecture | Modular monolith: `Modules/{Authors,Posts,Comments,Tags}` |
| Data access | EF Core **10** + Npgsql |
| Database | PostgreSQL **18** (shared Docker `masterTest`, port `55432`) |
| Client | Blazor WebAssembly (.NET 10) + Strawberry Shake **16.6.6** |

Code-first means the schema is *generated from C#*: query methods become fields, entity classes become types, and EF configurations shape the data. There is no SDL file you hand-write — the SDL is an output you can download.

## What a schema is

A schema is the typed contract between server and client. It declares object **types**, the **fields** on them, the **arguments** those fields accept, and the **operations** you can run (queries, mutations, subscriptions). Because it is typed, an editor like Nitro can autocomplete it and a code generator like Strawberry Shake can turn it into C# classes. When the schema changes, both ends learn about it.

## Everything is a connection

Every list in this repo is a Relay-style **connection**: `posts`, `authors`, `tags`, and the nested `Author.posts`, `BlogPost.comments`, and `BlogPost.tags`. A connection exposes `nodes`, `edges`, `pageInfo`, and `totalCount`, and takes `first`/`after` (or `last`/`before`) for cursor paging.

The key rule is in `Program.cs`: `ModifyPagingOptions(options => options.RequirePagingBoundaries = true)`. That means **every connection must be given `first` or `last`**. Omit both and the request fails with `HC0082: Exactly one slicing argument must be defined`. It sounds strict; it protects the database from an unbounded `SELECT *`. Nested lists add their own caps — `Author.posts` is `MaxPageSize` 10, `BlogPost.comments` 50, and `BlogPost.tags` 20 — and going over gives `HC0051`. Connections also carry `[UseFiltering]` and `[UseSorting]`, so each list takes optional `where` and `order`.

## Every id is a global Node id

`AddGlobalObjectIdentification()` turns on the Relay Global Object Identification spec. Three consequences follow:

1. Every entity implements the `Node` interface and its `id` is `ID!` — not a database key.
2. The id is an opaque string: it is `base64("Type:localId")`. `BlogPost:1` becomes `QmxvZ1Bvc3Q6MQ==`, `Author:1` becomes `QXV0aG9yOjE=`, `Tag:1` becomes `VGFnOjE=`.
3. A single query field re-fetches anything: `node(id: ID!)`, or `nodes(ids: [ID!]!)` for many at once.

You never construct an id by hand; you read one back and pass it in. Inputs use global ids too. The legacy `authorById`/`postById` fields still exist but are `@deprecated` — `node(id:)` is the supported path. The resolver that re-fetches a node lives in `Modules/*/*NodeResolver.cs`, and each model points at it with `[Node(NodeResolverType = …, NodeResolver = …)]`.

## Walking the schema

Open `http://localhost:5100/graphql` in a browser and you get **Nitro**, the built-in IDE; `http://localhost:5100/graphql/ui` serves it on its own path. The full SDL is at `http://localhost:5100/graphql/schema`. Here is the curated shape from the [README](../README.md#the-graphql-schema-at-a-glance):

```graphql
type Query {
  authors(first: Int, after: String, where: AuthorFilterInput, order: [AuthorSortInput!]): AuthorConnection
  posts(first: Int, after: String, where: BlogPostFilterInput, order: [BlogPostSortInput!]): BlogPostConnection
  tags(first: Int, after: String, where: TagFilterInput, order: [TagSortInput!]): TagConnection
  node(id: ID!): Node
  nodes(ids: [ID!]!): [Node]!
  authorById(id: Int!): Author @deprecated(reason: "Use the node(id: ID!) field instead.")
  postById(id: Int!): BlogPost @deprecated(reason: "Use the node(id: ID!) field instead.")
}

interface Node {
  id: ID!
}

type BlogPost implements Node {
  id: ID!
  title: String!
  slug: String!
  body: String!
  status: PostStatus!
  createdAt: DateTime!
  publishedAt: DateTime
  authorId: Int!
  author: Author!
  comments(first: Int, after: String, where: CommentFilterInput, order: [CommentSortInput!]): CommentConnection!
  tags(first: Int, after: String, where: TagFilterInput, order: [TagSortInput!]): TagConnection!
}
```

Read it as a narrator would: query one list (`posts(first: 5)`), then drill into a `BlogPost`'s `author`, `comments`, and `tags`. Notice the connection triple on every nested list, and that `BlogPost.author` is a single object resolved by a DataLoader rather than a page. That is the whole browsing model.

## Where each feature lives

The repo is a modular monolith: each module owns its queries, mutations, object type, and EF configuration.

| Concept | Where to look |
|---|---|
| Query root + connections, filtering/sorting | `src/GraphQLPractice.Api/Modules/{Posts,Authors,Tags}/*Queries.cs` |
| Mutations + typed errors | `Modules/{Authors,Posts,Comments,Tags}/*Mutations.cs` |
| Subscriptions + dynamic topics | `Modules/Posts/PostSubscription.cs`, `Modules/Comments/CommentSubscription.cs` |
| Global object identification (`Node`, node resolvers) | `Modules/*/*NodeResolver.cs`, `Modules/*/*Node.cs` |
| DataLoaders (N+1 batching) | `Modules/Authors/DataLoaders`, `Modules/Posts/DataLoaders` |
| Server wiring (modules, GraphQL, CORS, cost, paging) | `src/GraphQLPractice.Api/Program.cs` |
| Client operations and pages | `src/GraphQLPractice.Client/GraphQL/*.graphql`, `src/GraphQLPractice.Client/Pages/*.razor` |

`IModule.cs` and `ModuleRegistry.cs` define how a module registers itself; `Program.cs` loops over `ModuleRegistry.Modules` and calls `Register` on each. The [API README](../src/GraphQLPractice.Api/README.md) drills into this layout.

## Try it yourself

With the API running at `http://localhost:5100/graphql`, work through these in Nitro. This is the "the API documents itself" moment.

```graphql
{
  __schema {
    queryType { name }
    mutationType { name }
    subscriptionType { name }
  }
}
```

1. Run the introspection query above — you should see the three root operation type names come back. The schema is describing itself.
2. Ask for a specific type: `{ __type(name: "BlogPost") { fields { name type { kind name ofType { kind name } } } } }`. This is exactly how Nitro builds its Schema panel.
3. Now query the real thing. Put these in the Request and Variables panes:

```graphql
query ListPosts($first: Int!, $where: BlogPostFilterInput) {
  posts(first: $first, where: $where) {
    totalCount
    nodes {
      id
      title
      status
      author { name }
      tags(first: 5) { nodes { name } }
    }
  }
}
```

```json
{ "first": 3, "where": { "status": { "eq": "PUBLISHED" } } }
```

4. Copy one returned `id` (something like `QmxvZ1Bvc3Q6MQ==`) and round-trip it through the global node field:

```graphql
query ByNode($id: ID!) {
  node(id: $id) {
    id
    ... on BlogPost { title status author { name } }
  }
}
```

```json
{ "id": "QmxvZ1Bvc3Q6MQ==" }
```

5. Finally, prove the paging rule: delete `first` from the `posts` call and run it. You get `HC0082: Exactly one slicing argument must be defined`. Put it back — the schema was enforcing the contract all along.

## Key takeaways

- A schema is a typed contract; code-first means it is generated from C#, and introspection is how tools read it.
- Every list is a connection, and `RequirePagingBoundaries = true` means you must always pass `first` or `last`.
- Global object identification makes every `id` an opaque `ID!` string (`base64("Type:localId")`), re-fetchable through `node(id:)`.
- The domain is small on purpose; the point is the GraphQL mechanics, not the business logic.
- Nitro at `/graphql` lets you explore all of it without writing a client.

## Where to go next

- Run the stack first: [Post 00 — Local setup](00-local-setup.md)
- The full guided feature tour: [graphql-tour.md](../docs/graphql-tour.md)
- Environment and tooling detail: [dev-setup.md](../docs/dev-setup.md)
- Repo overview and quick start: [README.md](../README.md)
- Server internals and module layout: [GraphQLPractice.Api README](../src/GraphQLPractice.Api/README.md)
