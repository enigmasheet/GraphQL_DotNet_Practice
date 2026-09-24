# A guided GraphQL tour

Everything below can be pasted into the **Nitro IDE** at <http://localhost:5100/graphql> while the
API is running. The API console prints the SQL EF Core executes, so keep an eye on it.

> Not running yet? See [dev-setup.md](dev-setup.md).

## Using Nitro

Nitro is the GraphQL IDE built into Hot Chocolate. Start the API:

```powershell
dotnet run --project src\GraphQLPractice.Api
```

Then open one of:

- <http://localhost:5100/graphql> — the endpoint; a browser gets Nitro
- <http://localhost:5100/graphql/ui> — the IDE on its own path
- <http://localhost:5100/graphql/schema> — the schema SDL

**First run:** click **Create Document**, confirm the endpoint is `http://localhost:5100/graphql`,
and click **Apply**. When the bottom-right says *Schema available*, introspection worked.

**The panels:**

| Area | What it is for |
|---|---|
| Request | Write the operation; **Run** (or `Ctrl+Enter`) executes it |
| Variables | JSON variable values for the current document (see §2) |
| Response | JSON result, including any `errors` (see §10) |
| Headers / Network | HTTP status, response headers, timing |
| Schema | Browse types, or read the raw SDL |
| History / Settings | Re-run past operations; rename or duplicate documents |

> **"Fusion Operation Plan Not Supported"?** That tab belongs to **Fusion** — a gateway
> (`AddGraphQLGateway()`) that federates several subgraphs and has a query planner. This project is a
> single Hot Chocolate graph (`AddGraphQL()`), so there is no plan to show and Nitro says so. It is
> **expected, not a misconfiguration**. For plan-like insight here, watch the API console: it logs
> every SQL statement, which is how you see DataLoaders collapse N+1 into one query (§11).

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

Never string-concatenate inputs; use variables. Nitro keeps variables per document in the
**Variables** pane.

**Scalars and enums.** GraphQL enums are JSON strings; `Int` arguments need a number, not a quoted
string:

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

**Input objects.** A whole `input` in one variable:

```graphql
mutation Create($input: CreatePostInput!) {
  createPost(input: $input) { blogPost { id slug } errors { __typename } }
}
```

```json
{
  "input": {
    "title": "Hello",
    "body": "...",
    "authorId": 1,
    "status": "DRAFT",
    "tagIds": [1, 2]
  }
}
```

**Filter and sort** as variables (cleaner than inlining `where`):

```graphql
query List($where: BlogPostFilterInput, $order: [BlogPostSortInput!]) {
  posts(first: 5, where: $where, order: $order) { nodes { title status } }
}
```

```json
{ "where": { "status": { "eq": "PUBLISHED" } }, "order": [{ "createdAt": "DESC" }] }
```

Things that trip people up:

- `postById(id: Int!)` needs the raw key, `2`, not `"2"`. `node(id: ID!)` is the opposite: it takes
  the opaque global id **string** (`"QmxvZ1Bvc3Q6Mg=="`). GraphQL `ID` is a string scalar — the
  difference is the encoding, not the JSON type.
- Non-null (`!`) variables must always be supplied; nullable ones can be omitted entirely to mean
  "no filter".
- **Omitting a variable is not the same as sending `null`.** `{ "where": { "status": { "eq": null } } }`
  matches posts whose status is null (i.e. none) — not "all statuses". To ignore a filter, omit it.
- Variables work for subscriptions too; changing `postId` changes the topic (§9).

## 3. Nested fields and resolvers

GraphQL resolves nested objects through resolvers, not one giant query:

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

- `BlogPost.author`, `Comment.author` and `Comment.post` are served by the `[ObjectType<T>]` node
  classes via **DataLoaders** (see §11). `Author.posts`, `BlogPost.comments` and `BlogPost.tags` are
  paged connections resolved straight from EF with `IQueryable` (see §4).
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

Start with `after: null`, then pass the previous `endCursor`. Every list in this schema is a
connection built with `[UseConnection]`, and `RequirePagingBoundaries` is on, so a client must always
pass `first` (or `last`) — omitting it fails with `HC0082` (§10). `MaxPageSize` bounds the page.

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

Paged fields use `QueryContext<T>`, whose `Selector` is built from your selection set, so EF Core
only reads the columns you asked for:

```graphql
{ tags(first: 2) { nodes { name } } }          # SELECT t."Name" FROM "Tags" AS t …
{ tags(first: 2) { nodes { id name } } }       # SELECT t."Id", t."Name" FROM "Tags" AS t …
```

Watch the API console and compare. (`QueryContext` replaces the older `[UseProjection]` attribute for
paged fields — do not combine the two on one field; the `HC0099` analyzer warns.)

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
subscription OnCommentAdded($postId: ID!) {
  onCommentAdded(postId: $postId) { id text author { name } }
}
```

```json
{ "postId": "QmxvZ1Bvc3Q6Mg==" }
```

`postId` is a global id (`[ID(nameof(BlogPost))]`); Hot Chocolate decodes it to the local key before
it is substituted into the dynamic topic `OnCommentAdded_{postId}`, which is exactly the topic the
`addComment` mutation publishes to — so the subscription fires only for that post.

Leave it open, then run `addComment` for post `2` in another tab. `onCommentAdded` uses a
**dynamic topic** (`OnCommentAdded_{postId}`), so only subscribers for that post receive the event.

`onPostPublished` works the same way for newly published posts.

## 10. Debugging

### Two channels for "errors"

| Channel | Where | Examples |
|---|---|---|
| Request / execution errors | top-level `errors` | invalid JSON, unknown field, wrong value type, page size, non-null violations |
| Domain errors | the mutation payload's `errors` field | `SlugAlreadyInUseError`, `AuthorNotFoundError` |

A duplicate post title is a **successful** response that contains a payload `errors` entry — not a
transport error. Always select `errors { __typename ... }` on a payload.

### HTTP status is a clue

- **400** — the *request* is wrong. These errors carry no `path` (they failed before execution):
  malformed JSON, unknown field, wrong value type.
- **200** — execution happened. Field errors have a `path`, and `data` can contain `null` at that
  field. This includes `HC0051` (page size) and all typed domain errors.

### Reading an error

`errors[].message`, `errors[].path` (which field), `errors[].locations` (which line/column),
`errors[].extensions.code`. In Development, `IncludeExceptionDetails = true` (see `Program.cs`) also
adds `extensions.exception.stackTrace` — that is why you see stack traces locally. It is masked in
Production, so debug locally.

### Try each of these (responses captured from this API)

| Operation | Status | Result |
|---|---|---|
| body `{"query": ` (malformed JSON) | 400 | `"Invalid JSON document."` — `extensions.code: "HC0012"` |
| `{ posts { nodes { nope } } }` | 400 | ``The field `nope` does not exist on the type `BlogPost`.`` + `locations` |
| `{ posts(where: { status: { eq: PUBLSHED } }) { nodes { id } } }` | 400 | ``"The specified value type of field `eq` does not match the field type."`` (`fieldType: PostStatus`) |
| `{ posts(first: 100) { nodes { id } } }` | 200 | `"The maximum allowed items per page were exceeded."` — `HC0051`, `maxAllowedItems: 50`, `data.posts: null` |
| `createPost(input: { …, authorId: 99999 })` | 200 | payload `errors: [{ __typename: "AuthorNotFoundError", message: "Author with id 99999 was not found." }]` |
| `{ postById(id: 99999) { id title } }` | 200 | no error — `data.postById` is simply `null` |
| `{ posts { nodes { id } } }` (no `first`) | 200 | `"Exactly one slicing argument must be defined."` — `HC0082` |
| a query estimated over the cost budget | 200 | `"The maximum allowed field cost was exceeded."` — `HC0047` |

The last row is the important habit: **a missing object is `null`, not an error**, so write queries
that tolerate nullability (the client's `postById` field is nullable for exactly this reason).

### Cost analysis (HC0047)

Hot Chocolate estimates an operation's cost before running it and rejects anything over budget. Two
independent budgets:

- **field cost** — resolver/input work. This is the one that usually trips first.
- **type cost** — objects in the response.

Hot Chocolate's defaults are `MaxFieldCost = 1_000` and `MaxTypeCost = 10_000`; this project sets
both to `10_000` (see the note below).

Cost **multiplies at every list nesting level**, and some fields are expensive:

| Element | Weight |
|---|---|
| Scalars/enums (`id`, `title`, `totalCount`) | 0 |
| Fields returning an object | 1 |
| Fields without a pure resolver (DataLoader-backed `author`, `comments`, …) | 10 |
| A paged list | its `first` value (or `DefaultPageSize`); `MaxPageSize` caps it |

Because every list here is a connection with an explicit `first`, the numbers stay small — the
nested query in §3 reports `fieldCost` in the low hundreds, well under 1000.

Measure any operation with the built-in header (Nitro: **Settings → HTTP headers**):

```
GraphQL-Cost: report     # executes and returns extensions.operationCost { fieldCost, typeCost }
GraphQL-Cost: validate   # measures without executing
```

If a legitimate operation is rejected: bound it more tightly (`first: 5`), select less, or raise the
budget with `.ModifyCostOptions(o => o.MaxFieldCost = 20_000)` — but prefer bounding lists first.

> **Why the budget here is 10,000, not 1,000.** Hot Chocolate prices a **variable-bound** filter or
> sort input at its worst case, so a normal client call like `posts(first: 5, where: $where, …)` —
> even with `where: null` — is estimated far above its real cost (≈2,400 for `BlogPostFilterInput`
> alone, vs ≈20 when the same filter is written as a literal). `Program.cs` therefore raises
> `MaxFieldCost`/`MaxTypeCost` to `10_000`, which lets real client operations through while the
> analyzer still blocks genuinely expensive ones. If you want the tight default back, avoid
> variable-bound filters, or shrink the filter input types.

> HTTP status depends on `Accept`: `400` for `application/graphql-response+json` (curl), `200` for
> `application/json`. Cost rejections and `HC0082` are field errors and come back as `200` here.

### Other places to look

- **Server console** — EF Core SQL per request, request logs, seeding. Watch it while running §11 to
  see DataLoaders batch.
- **Schema tab** — confirm the exact field/argument names before blaming the query.
- **Subscriptions** — keep the tab open (it shows a live state instead of returning). The `addComment`
  mutation must use the **same `postId`** as the subscription, or it is a different topic and nothing
  arrives.
- **Isolate from the IDE** with a raw request:

  ```powershell
  curl.exe -s -X POST http://localhost:5100/graphql -H "Content-Type: application/json" `
    --data '{\"query\":\"{ postById(id: 99999) { id } }\"}'
  ```

## 11. The N+1 problem and DataLoaders

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

## 12. Scalars

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

> This project does not currently add any custom scalars — it is a good first exercise (§14).

## 13. Introspection

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

## 14. Exercises

1. **Add a scalar.** Create a `SlugType` (`RegexType`) for `[a-z0-9]+(-[a-z0-9]+)*`, register it
   with `.AddType<SlugType>()`, and annotate a field. Try an invalid value and watch coercion fail.
2. **Add a field.** Expose `BlogPost.commentCount` and resolve it with a DataLoader. Confirm it is
   batched in the SQL log.
3. **Add a mutation.** Implement `publishPost(id)` and publish to `onPostPublished`.
4. **Add filtering** to the nested `BlogPost.comments` field with `[UseFiltering]`.
5. **Client-side.** Add an `updatePost` form to the Blazor client and surface the `SlugAlreadyInUseError`.

## 15. Global object identification (Relay `Node`)

`AddGlobalObjectIdentification()` (in `Program.cs`) enables the [Relay Global Object Identification
spec](https://relay.dev/graphql/objectidentification.htm). Three things change:

**1. `id` becomes a global `ID`.** Every entity is decorated with `[Node]`, so `id` is no longer the
database key — it is an opaque, globally-unique value. `BlogPost.id` is now `ID!`:

```graphql
{ posts(first: 1) { nodes { id title } } }
# => { "id": "QmxvZ1Bvc3Q6MQ==", "title": "…" }
```

That value is just `base64("BlogPost:1")`. You never construct it by hand; you pass it back.

**2. Any node is fetchable by id.** The `Query` type gains a `Node` interface and two fields:

```graphql
{
  node(id: "QmxvZ1Bvc3Q6MQ==") {
    id
    ... on BlogPost { title author { name } }
    ... on Author { name }
  }
}
```

```graphql
{ nodes(ids: ["QmxvZ1Bvc3Q6MQ==", "QXV0aG9yOjE="]) { ... on BlogPost { title } ... on Author { name } } }
```

Each module supplies a node resolver — e.g. `PostNodeResolver.GetPostAsync` in
`Modules/Posts/PostNodeResolver.cs` — which Hot Chocolate dispatches to based on the type encoded in
the id. A node resolver must live in a class that is **not** a `[QueryType]`; if it were, the method
would also be exposed as an extra `Query` field (and `[GraphQLIgnore]` would stop it being registered
as a node resolver at all). The model points at it explicitly:

```csharp
[Node(
    NodeResolverType = typeof(Modules.Posts.PostNodeResolver),
    NodeResolver = nameof(Modules.Posts.PostNodeResolver.GetPostAsync)
)]
public sealed class BlogPost { … }
```

**3. Inputs take global ids.** With global object identification on, the `[ID]` attribute on a
parameter both marks it as `ID` *and* deserializes it back to the underlying key:

```csharp
public static async Task<BlogPost> UpdatePostAsync(
    [ID(nameof(BlogPost))] int id,   // client sends "QmxvZ1Bvc3Q6MQ==", EF gets 1
    string? title, …)
```

The optional type name (`[ID(nameof(BlogPost))]`) rejects ids that belong to a different type.

The old `authorById`/`postById` fields still work but are `[GraphQLDeprecated]` — prefer `node(id:)`.

> Cost note: the `Node` interface and `node`/`nodes` fields are included in the schema, so
> introspection-based cost reports grow slightly. That is expected; see [§10](#10-debugging).

Exercises:

1. Fetch a post's `id`, then round-trip it through `node(id:)`. Confirm you get the same post.
2. Pass an *author* id to `nodes(ids:)` and confirm the `Author` fragment resolves.
3. Change `UpdatePostAsync` to `[ID] int id` (drop the type name) and try an author id — what happens?

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
