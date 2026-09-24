# A guided GraphQL tour

Everything below can be pasted into the **Nitro IDE** at <http://localhost:5100/graphql> while the
API is running. The API console prints the SQL EF Core executes, so keep an eye on it.

> Not running yet? See [dev-setup.md](dev-setup.md).

## 1. Fields and aliases

Ask for exactly what you want. Aliases let you request the same field twice:

```graphql
{
  posts(first: 3) {
    nodes {
      id
      title
      status
    }
  }
}
```

```graphql
{
  published: posts(first: 2, where: { status: { eq: PUBLISHED } }) {
    nodes { title }
  }
  drafts: posts(first: 2, where: { status: { eq: DRAFT } }) {
    nodes { title }
  }
}
```

## 2. Variables

Never string-concatenate inputs; use variables. Nitro has a **Variables** pane:

```graphql
query PostsByStatus($status: PostStatus!, $first: Int!) {
  posts(first: $first, where: { status: { eq: $status } }) {
    totalCount
    nodes { id title }
  }
}
```

```json
{ "status": "PUBLISHED", "first": 2 }
```

## 3. Nested fields and resolvers

GraphQL resolves nested objects through resolvers, not one giant query:

```graphql
{
  posts(first: 3) {
    nodes {
      title
      author { name }
      tags { name }
      comments {
        text
        author { name }
        post { title }
      }
    }
  }
}
```

- `BlogPost.author`, `BlogPost.comments`, `BlogPost.tags`, `Comment.author` and `Comment.post` are
  served by the `[ObjectType<T>]` classes in `GraphQL/Types/` via **DataLoaders** (see §9).
- The raw EF navigation properties are hidden with `[GraphQLIgnore]` so there is exactly one way to
  get each field.

## 4. Cursor pagination (Relay connections)

`posts` and `authors` are Relay connections: `nodes` for the data, `pageInfo` for cursors.

```graphql
query Page($after: String) {
  posts(first: 2, after: $after) {
    totalCount
    pageInfo { hasNextPage endCursor }
    edges { cursor node { id title } }
  }
}
```

Start with `after: null`, then pass the previous `endCursor`. This page uses `[UsePaging]`, so a
client can never ask for an unbounded list (`MaxPageSize = 50`).

## 5. Filtering

Filtering is generated per type (`BlogPostFilterInput`, `AuthorFilterInput`, …):

```graphql
{
  posts(where: { title: { contains: "graphql" }, status: { eq: PUBLISHED } }) {
    nodes { title }
  }
}
```

Operators include `eq`, `neq`, `in`, `nin`, `contains`, `startsWith`, `gt`, `lt`, and `and`/`or`:

```graphql
{
  posts(where: { or: [ { title: { contains: "query" } }, { body: { contains: "query" } } ] }) {
    nodes { title }
  }
}
```

## 6. Sorting

`order` takes a list of sort inputs, applied in order:

```graphql
{
  posts(order: [ { status: ASC }, { createdAt: DESC } ]) {
    nodes { status createdAt title }
  }
}
```

## 7. Projections

The `tags` query uses `[UseProjection]`. Projection pushes your selection set into SQL, so EF Core
only reads the columns you asked for:

```graphql
{ tags { name } }          # SELECT t."Name" FROM "Tags" AS t
{ tags { id name } }       # SELECT t."Id", t."Name" FROM "Tags" AS t
```

Watch the API console and compare. Because it is server-driven and strongly typed, GraphQL lets the
database do less work for the same response shape.

## 8. Mutations and typed errors

Mutations use Hot Chocolate's mutation conventions: one `input` argument and a payload with the data
plus an `errors` union. The post payload field is `blogPost`:

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
    "authorId": 1,
    "status": "PUBLISHED",
    "tagIds": [1, 2]
  }
}
```

Run it twice with the same title: the second call returns a `SlugAlreadyInUseError` in `errors`
instead of a transport error. That is the point of typed errors — clients can branch on them.

Other mutations:

```graphql
mutation { updatePost(input: { id: 1, title: "Renamed" }) { blogPost { id title slug } errors { __typename } } }
mutation { deletePost(input: { id: 1 }) { blogPost { id title } errors { __typename } } }
mutation { createAuthor(input: { name: "New Author", bio: "Hi" }) { author { id name } } }
mutation { addComment(input: { postId: 2, authorId: 1, text: "Nice!" }) { comment { id text } errors { __typename } } }
```

## 9. Subscriptions

Open a subscription in Nitro (Nitro uses WebSocket automatically):

```graphql
subscription OnCommentAdded($postId: Int!) {
  onCommentAdded(postId: $postId) { id text author { name } }
}
```

```json
{ "postId": 2 }
```

Leave it open, then run `addComment` for post `2` in another tab. `onCommentAdded` uses a
**dynamic topic** (`OnCommentAdded_{postId}`), so only subscribers for that post receive the event.

`onPostPublished` works the same way for newly published posts.

## 10. The N+1 problem and DataLoaders

Naive GraphQL fetches a related object once per parent: `N` parents → `N+1` queries. DataLoaders
batch and cache lookups **within a single request**.

Watch the API console with:

```graphql
{
  posts(first: 5) {
    nodes { title author { name } }
  }
}
```

You will see one query for the posts, then exactly one batched query for authors
(`WHERE a."Id" = ANY (@ids)`). Remove `author { name }` and repeat: the author query disappears.

The loaders live in `GraphQL/DataLoaders/`:

| Loader | Kind | Used by |
|---|---|---|
| `AuthorByIdDataLoader` | batch (one per key) | `BlogPost.author`, `Comment.author` |
| `PostByIdDataLoader` | batch | `Comment.post` |
| `PostsByAuthorIdDataLoader` | group (many per key) | `Author.posts` |
| `CommentsByPostIdDataLoader` | group | `BlogPost.comments` |
| `TagsByPostIdDataLoader` | group (across the join table) | `BlogPost.tags` |

They are source-generated from `[DataLoader]` methods and registered by
`builder.Services.AddDataLoaders()` (see `GraphQL/ModuleInfo.cs`).

## 11. Scalars

A **scalar** is a GraphQL leaf type. Hot Chocolate maps many .NET types automatically and only
emits the ones your schema uses:

| .NET | GraphQL |
|---|---|
| `string`, `int`, `bool`, `double` | `String`, `Int`, `Boolean`, `Float` |
| `DateTimeOffset` | `DateTime` |
| `Guid` | `UUID` |
| `DateOnly` | `LocalDate` |
| `decimal`, `long`, `TimeSpan` | `Decimal`, `Long`, `Duration` |

Custom scalars are created by deriving from `ScalarType<TRuntime, TLiteral>` (or the helpers
`RegexType`, `IntegerTypeBase<T>`, `FloatTypeBase<T>`) and registered with `.AddType<T>()`. Ready-made
scalars such as `EmailAddress`, `HexColor`, `IPv4`, `Latitude`, `Longitude` and `UtcOffset` come
from the `HotChocolate.Types.Scalars` package.

> This project does not currently add any custom scalars — it is a good first exercise (§13).
> Note: a GraphQL **scalar** is unrelated to the **Scalar** OpenAPI UI.

## 12. Introspection

GraphQL is self-describing, which is how Nitro and the Strawberry Shake client know your schema:

```graphql
{
  __schema { queryType { name } mutationType { name } subscriptionType { name } }
}
```

```graphql
{
  __type(name: "BlogPost") {
    fields { name type { kind name ofType { kind name } } }
  }
}
```

## 13. Exercises

1. **Add a scalar.** Create a `SlugType` (`RegexType`) for `[a-z0-9]+(-[a-z0-9]+)*`, register it
   with `.AddType<SlugType>()`, and annotate a field. Try an invalid value and watch coercion fail.
2. **Add a field.** Expose `BlogPost.commentCount` and resolve it with a DataLoader. Confirm it is
   batched in the SQL log.
3. **Add a mutation.** Implement `publishPost(id)` and publish to `onPostPublished`.
4. **Add filtering** to the nested `BlogPost.comments` field with `[UseFiltering]`.
5. **Client-side.** Add an `updatePost` form to the Blazor client and surface the `SlugAlreadyInUseError`.

## Client side (Strawberry Shake)

The Blazor client generates strongly-typed C# types from `.graphql` documents at build time.

- Operations live in `src/GraphQLPractice.Client/GraphQL/*.graphql`.
- After changing the server schema, re-pull and regenerate:

  ```powershell
  dotnet run --project src\GraphQLPractice.Api      # in one terminal
  dotnet graphql update -p src\GraphQLPractice.Client
  ```

- Two styles are used in `Pages/`:
  - **Declarative** — the generated `<UseGetAuthors>` component handles loading/error and gives you
    the result in `context` (`Authors.razor`).
  - **Imperative** — inject `IBlogClient` and call `Client.GetPosts.ExecuteAsync(...)`, then inspect
    `result.IsErrorResult()`, `result.Errors` and `result.Data` (`Posts.razor`, `PostDetail.razor`).

  `PostDetail.razor` also subscribes with `Client.OnCommentAdded.Watch(postId)` over WebSocket and
  pushes new comments into the UI live.
