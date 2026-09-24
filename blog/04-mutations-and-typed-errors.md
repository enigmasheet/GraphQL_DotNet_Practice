# Mutations and typed errors: commands that read like queries

> Part 4 of the GraphQL .NET Practice study series

You have spent three posts reading. Now you write. A GraphQL mutation looks like a query — same fields, same selection set — but it lands on a different root type, because changing data deserves different semantics. This post walks through every mutation in this repo, the one-input/one-payload convention that wraps them, and the typed-error pattern that turns a domain failure such as "that slug is taken" into data you can branch on instead of a transport error. By the end, a duplicate title will not look like a crash; it will look like a field.

## What you'll learn

- Why `Mutation` is a separate root operation type, and why writes "read like a query, write like a command"
- The mutation convention: one `input` argument in, one payload out
- Why each mutation returns the entity it changed
- The typed errors: `PostNotFoundError`, `AuthorNotFoundError`, `SlugAlreadyInUseError`, `TagNotFoundError`
- How `[Error(typeof(...))]` maps a thrown domain exception to a payload error
- Global `ID` inputs, and the three different places an "error" can appear

## Read like a query, write like a command

A query root and a mutation root are both plain object types. The difference is intent: `Query` is for reading, `Mutation` is for commands, and the server treats top-level mutation fields as a serial sequence rather than an interchangeable parallel set. In this schema (see `Program.cs`) that separation is wired with `.AddMutationConventions(applyToAllMutations: true)`:

```graphql
type Query { posts(first: Int): BlogPostConnection!, node(id: ID!): Node, … }

type Mutation {
  createAuthor(input: CreateAuthorInput!): CreateAuthorPayload!
  createPost(input: CreatePostInput!): CreatePostPayload!
  updatePost(input: UpdatePostInput!): UpdatePostPayload!
  deletePost(input: DeletePostInput!): DeletePostPayload!
  addComment(input: AddCommentInput!): AddCommentPayload!
  createTag(input: CreateTagInput!): CreateTagPayload!
  deleteTag(input: DeleteTagInput!): DeleteTagPayload!
}
```

"Read like a query" means a mutation selects fields exactly the way a query does — you still name the fields you want back. "Write like a command" means the server knows the primary intent is a state change: every command takes a single `input` object and answers with a single payload.

## One input in, one payload out

The C# methods in `Modules/{Authors,Posts,Comments,Tags}/*Mutations.cs` do not take an `input` object themselves. They take plain arguments, and the convention wraps them for you:

```csharp
public static async Task<BlogPost> CreatePostAsync(
    string title,
    string body,
    [ID(nameof(Author))] int authorId,
    PostStatus status,
    [ID(nameof(Tag))] int[]? tagIds,
    AppDbContext db,
    ITopicEventSender sender,
    CancellationToken ct)
```

From that method Hot Chocolate generates `CreatePostInput` and a payload whose field is named after the entity it returns:

```graphql
input CreatePostInput {
  title: String!
  body: String!
  authorId: ID!
  status: PostStatus!
  tagIds: [ID!]
}

type CreatePostPayload {
  blogPost: BlogPost
  errors: [CreatePostError!]
}

union CreatePostError = AuthorNotFoundError | SlugAlreadyInUseError
```

The payload field name follows the entity: `blogPost`, `author`, `comment`, `tag`. `CreateAuthorPayload` is the one exception — it has no `errors` field because `createAuthor` declares no error types.

## Return the thing you changed

Every mutation returns `Task<BlogPost>`, `Task<Comment>`, or `Task<Tag>` — and the payload exposes it. Returning the changed entity lets a client update its store from the mutation response with no follow-up query; `deletePost` even returns the deleted post so the client has the id and title it needs to remove. `createPost` additionally publishes the new post to a subscription when its status is `PUBLISHED` (the next post covers that).

## Domain failures are data

A missing author is not a broken server; it is a normal outcome the client must handle. The typed-error pattern puts that outcome in the payload's `errors` array. Each error type implements the `Error` interface (`message`) and adds typed fields taken from the domain exception's public properties:

```graphql
type PostNotFoundError implements Error { message: String! postId: Int! }
type AuthorNotFoundError implements Error { message: String! authorId: Int! }
type SlugAlreadyInUseError implements Error { message: String! slug: String! }
type TagNotFoundError implements Error { message: String! tagId: Int! }
```

The exceptions themselves are tiny classes under `Errors/`:

```csharp
public sealed class SlugAlreadyInUseException(string slug)
    : Exception($"A post with slug '{slug}' already exists.")
{
    public string Slug { get; } = slug;
}
```

The link between the two is the `[Error]` attribute on the method. It both declares which errors the payload union may contain and catches the exception before it escapes:

```csharp
[Error(typeof(AuthorNotFoundException))]
[Error(typeof(SlugAlreadyInUseException))]
public static async Task<BlogPost> CreatePostAsync(…) { … }

[Error(typeof(PostNotFoundException))]
[Error(typeof(SlugAlreadyInUseException))]
public static async Task<BlogPost> UpdatePostAsync(…) { … }
```

So a duplicate post title is a **successful** response (HTTP 200) whose payload is `{ blogPost: null, errors: [...] }` — never a top-level GraphQL `errors` entry. That is the whole point: clients branch on `__typename` and the typed fields (`slug`, `authorId`, `postId`, `tagId`) instead of parsing a message string.

## Global ids in, global ids out

`authorId` is `ID!` in `CreatePostInput`, but the C# parameter is `[ID(nameof(Author))] int authorId`. With global object identification on, `[ID]` accepts the opaque string (`QXV0aG9yOjE=`) and decodes it back to the local key (`Author:1` → `1`) before your code runs. The optional type name rejects an id that belongs to a different type instead of silently targeting the wrong row. `tagIds` and every `id` in `UpdatePostInput`/`DeletePostInput`/`AddCommentInput` work the same way.

## Three places an "error" can appear

Keep these channels distinct; most confusion comes from mixing them up.

| Channel | HTTP | Where it lives | Examples |
|---|---|---|---|
| Request / validation | **400** | top-level `errors`, no `path` | malformed JSON, unknown field (`HC0012`), wrong value type |
| Field (execution) | **200** | top-level `errors[].path` + `extensions.code` | paging boundary `HC0082`, page size `HC0051`, cost `HC0047` |
| Domain failure | **200** | the mutation payload's `errors` array | `SlugAlreadyInUseError`, `AuthorNotFoundError` |

And a fourth non-error: a missing single object is simply `null`. `postById(id: 99999)` returns `data.postById: null` with no `errors` at any level. Only mutations carry domain errors.

## Try it yourself

With the API running at `http://localhost:5100/graphql`, create a post, then run the identical mutation again.

```graphql
mutation CreatePost($input: CreatePostInput!) {
  createPost(input: $input) {
    blogPost { id title slug status }
    errors {
      __typename
      ... on SlugAlreadyInUseError { message slug }
      ... on AuthorNotFoundError { message authorId }
    }
  }
}
```

```json
{
  "input": {
    "title": "Hello GraphQL",
    "body": "My first post.",
    "authorId": "QXV0aG9yOjE=",
    "status": "PUBLISHED",
    "tagIds": ["VGFnOjE="]
  }
}
```

1. Run it once. `blogPost` comes back with a global `id`, a derived `slug` (`hello-graphql`), and `errors: []`.
2. Run it again unchanged. HTTP is still **200**; `blogPost` is `null` and `errors` contains a `SlugAlreadyInUseError` with `slug: "hello-graphql"`. The slug is re-derived from the title, so a duplicate title is a duplicate slug.
3. Trigger `AuthorNotFoundError` with a well-typed but nonexistent author:

```json
{ "input": { "title": "Orphan", "body": "x", "authorId": "QXV0aG9yOjk5OTk5", "status": "DRAFT" } }
```

4. Trigger `PostNotFoundError`:

```graphql
mutation { updatePost(input: { id: "QmxvZ1Bvc3Q6OTk5OTk=", title: "Nope" }) { blogPost { id } errors { __typename ... on PostNotFoundError { message postId } } } }
```

5. Trigger `TagNotFoundError` with `deleteTag(input: { id: "VGFnOjk5OTk5" })`, and `SlugAlreadyInUseError` by running `createTag` twice with the same name.
6. Add an unknown field to any selection (for example `blogPost { nope }`) and run it. Now you get **HTTP 400** with `extensions.code: "HC0012"` and no `path` — nothing executed. Compare that with the `200` responses above.

## Key takeaways

- `Mutation` is its own root type; mutations read like queries but are commands and run serially at the top level.
- The convention wraps each method's arguments into one `input` and returns one payload named after the entity.
- Returning the changed entity lets clients update local state without a second request.
- Domain failures are typed payload data (`__typename` + fields) on an HTTP 200 response, never top-level GraphQL errors.
- `[Error(typeof(...))]` maps a thrown exception to a payload error type; `[ID]` accepts and decodes global ids.
- Keep the channels straight: 400 = request-level, 200 + `path` = field error, 200 + payload `errors` = domain failure.

## Where to go next

- Continue with [Subscriptions and realtime](05-subscriptions-and-realtime.md).
- The mutation section of the classic tour: [graphql-tour.md §8](../docs/graphql-tour.md#8-mutations-and-typed-errors).
- Paging, filtering, and sorting in the previous post: [03-paging-filtering-sorting.md](03-paging-filtering-sorting.md).
- Full guided tour: [graphql-tour.md](../docs/graphql-tour.md).
- Server internals and module layout: [GraphQLPractice.Api README](../src/GraphQLPractice.Api/README.md).
