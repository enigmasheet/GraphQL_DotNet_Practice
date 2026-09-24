# Global object identification

> Part 7 of the GraphQL .NET Practice study series

Once a client caches objects by `id`, two different types that both use `id: 1` collide. Relay's Global Object Identification spec solves this by making every id globally unique and adding a `Node` interface with `node`/`nodes` fields. Hot Chocolate implements the whole spec with one call and a handful of attributes. This post shows how the schema changes, what the ids encode, how inputs decode them, and the node-resolver pitfall that bites everyone once.

## What you'll learn

- Why global ids exist and what they encode
- The `Node` interface and the `node`/`nodes` fields
- How `[Node]` turns `id` into `ID!` and serializes `base64("Type:localId")`
- Fetching nodes with inline fragments
- `[ID]` on arguments and input object fields, including type restriction
- The node-resolver pitfall: a separate class, not a `[QueryType]` method

## Why global ids exist

Without them, `Author.id` and `BlogPost.id` are both `Int`, and both can be `1`. A client that normalizes its cache by id — Relay, Apollo, Strawberry Shake — would confuse an author with a post. The fix is to make each id carry its type. This project enables the spec in `Program.cs`:

```csharp
.AddGlobalObjectIdentification(options => options.MaxAllowedNodeBatchSize = 50)
```

## The Node interface and the node fields

The `Query` type gains a `Node` interface and two fields:

```graphql
type Query {
  node(id: ID!): Node
  nodes(ids: [ID!]!): [Node]!
}

interface Node { id: ID! }
```

Every entity implements it. `BlogPost` in the SDL:

```graphql
type BlogPost implements Node { id: ID! title: String! }
```

Each module decorates its model with `[Node(...)]`, and the `id` field is rewritten to `ID!`. The value is no longer the database key — it is opaque and globally unique.

## What a global id encodes

A global id is just base64 of `"Type:localId"`. From this repo's seed data:

| Entity | Local key | Global id |
|---|---|---|
| BlogPost | 1 | `QmxvZ1Bvc3Q6MQ==` |
| Author | 1 | `QXV0aG9yOjE=` |
| Tag | 1 | `VGFnOjE=` |
| Comment | 1 | `Q29tbWVudDox` |

Ask for it and you get the encoded string:

```graphql
{ posts(first: 1) { nodes { id title } } }
```

The response's `id` is `"QmxvZ1Bvc3Q6MQ=="`. You never build one by hand; the server mints it and you pass it back.

## Fetching a node with inline fragments

`node` returns the `Node` interface, so you select the shared `id` plus a fragment per concrete type:

```graphql
{
  node(id: "QmxvZ1Bvc3Q6MQ==") {
    id
    ... on BlogPost { title author { name } }
    ... on Author { name }
  }
}
```

`nodes` fetches many at once (up to `MaxAllowedNodeBatchSize`, 50 here):

```graphql
{ nodes(ids: ["QmxvZ1Bvc3Q6MQ==", "QXV0aG9yOjE="]) { ... on BlogPost { title } ... on Author { name } } }
```

The client uses exactly this. `GetPostById.graphql` fetches `node(id: $id)` with an inline fragment, and `PostDetail.razor` casts the result:

```csharp
post = postResult.Data?.Node as IGetPostById_Node_BlogPost;
```

The route `@page "/posts/{Id}"` treats the id as a string, because a global id is a string.

## Inputs take global ids

`[ID]` on a parameter does double duty: it marks the argument as `ID` and deserializes it back to the underlying key. In `PostMutations`:

```csharp
// client sends "QmxvZ1Bvc3Q6MQ==", EF gets 1
public static async Task<BlogPost> UpdatePostAsync([ID(nameof(BlogPost))] int id, string? title, ...)
```

The optional type name restricts what is accepted: `[ID(nameof(BlogPost))]` rejects an id that belongs to another type. Drop the name (`[ID] int id`) and any type's id decodes to whatever integer it holds — useful to know when debugging.

Mutations accept global ids throughout:

```graphql
mutation {
  createPost(input: { title: "Hi", body: "…", authorId: "QXV0aG9yOjE=", tagIds: ["VGFnOjE="] })
  updatePost(input: { id: "QmxvZ1Bvc3Q6MQ==", title: "Renamed" })
  deletePost(input: { id: "QmxvZ1Bvc3Q6MQ==" })
  addComment(input: { postId: "QmxvZ1Bvc3Q6MQ==", authorId: "QXV0aG9yOjE=", text: "Nice" })
  deleteTag(input: { id: "VGFnOjE=" })
}
```

The `[ID]` attribute works on input object fields too, so nested ids decode the same way.

## The node resolver pitfall

A node resolver is the method Hot Chocolate calls to re-fetch a type by its local key. It lives in a class named `*NodeResolver` — one per module — and the model points at it:

```csharp
[Node(
    NodeResolverType = typeof(Modules.Posts.PostNodeResolver),
    NodeResolver = nameof(Modules.Posts.PostNodeResolver.GetPostAsync)
)]
public sealed class BlogPost { … }
```

```csharp
public static Task<BlogPost?> GetPostAsync(int id, AppDbContext db, CancellationToken ct) =>
    db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
```

Two mistakes to avoid:

- **Don't put the resolver in a `[QueryType]` class.** If `GetPostAsync` sat in `PostQueries`, it would also be exposed as an extra `Query` field (like `resolvePost`), leaking an internal method into the public API.
- **Don't mark it `[GraphQLIgnore]`.** Ignoring it removes it from node resolution, and the schema build fails: "The type `BlogPost` implements the node interface but does not provide a node resolver for re-fetching."

The class is deliberately not a `[QueryType]`; the model's `[Node(...)]` attributes are the only reference to it.

Finally, the old integer-keyed fields still exist but are deprecated:

```graphql
authorById(id: Int!): Author @deprecated(reason: "Use the node(id: ID!) field instead.")
postById(id: Int!): BlogPost @deprecated(reason: "Use the node(id: ID!) field instead.")
```

Prefer `node(id: ID!)`.

## Try it yourself

1. Start the API and open <http://localhost:5100/graphql>.
2. Fetch a post id:
   ```graphql
   { posts(first: 1) { nodes { id title } } }
   ```
3. Round-trip it through `node`:
   ```graphql
   query GetNode($id: ID!) {
     node(id: $id) { id ... on BlogPost { title author { name } } }
   }
   ```
   ```json
   { "id": "QmxvZ1Bvc3Q6MQ==" }
   ```
4. Pass an author id to `nodes` and confirm the `Author` fragment resolves:
   ```graphql
   { nodes(ids: ["QXV0aG9yOjE="]) { ... on Author { name } } }
   ```
5. Try a wrong-type id: `node(id: "QXV0aG9yOjE=")` still resolves the author, but passing an author id to `updatePost` fails the `[ID(nameof(BlogPost))]` restriction. Confirm the deprecation in the SDL at <http://localhost:5100/graphql/schema>.

## Key takeaways

- Global ids prevent cache collisions by encoding `base64("Type:localId")`.
- `AddGlobalObjectIdentification` adds the `Node` interface and `node`/`nodes` fields, and rewrites entity `id` to `ID!`.
- Fetch nodes with inline fragments, because `node` returns the interface.
- `[ID(nameof(T))]` marks an argument or input field as a global id, decodes it to the raw key, and rejects ids of other types.
- A node resolver must be a method in a separate `*NodeResolver` class referenced by `[Node(NodeResolverType = ..., NodeResolver = nameof(...))]` — not a `[QueryType]` method and not `[GraphQLIgnore]`.
- `authorById`/`postById` are deprecated; use `node(id: ID!)`.

## Where to go next

- DataLoaders and N+1: [06-dataloaders-and-the-n-plus-1-problem.md](06-dataloaders-and-the-n-plus-1-problem.md)
- The typed Blazor client: [08-the-blazor-client.md](08-the-blazor-client.md)
- Global object identification in the tour: [../docs/graphql-tour.md](../docs/graphql-tour.md)
- Client's `node` usage: [../src/GraphQLPractice.Client/README.md](../src/GraphQLPractice.Client/README.md)
- Project README: [../README.md](../README.md)
