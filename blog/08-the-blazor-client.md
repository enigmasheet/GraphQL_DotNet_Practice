# The Blazor client: a typed GraphQL client from `.graphql` files

> Part 8 of the GraphQL .NET Practice study series

You have been hand-writing operations in Nitro for seven posts. The client does not: you drop a
`.graphql` document in a folder, build, and Strawberry Shake generates typed C# operations, results
and Razor components. This post walks the config and both calling styles, then the global-id round trip.

## What you'll learn

- How `.graphqlrc.json` controls the namespace, transports and schema source.
- What Strawberry Shake emits: `IBlogClient`, result interfaces, `<Use…>` components.
- Binding the GraphQL URL with the options pattern and injecting it into both transports.
- The imperative (`ExecuteAsync`) and declarative (`<UseGetAuthors>`) calling styles.
- Following a global `ID` from a query into `node(id:)` and into a mutation input.

## Configuration: `.graphqlrc.json` and `Program.cs`

`.graphqlrc.json` fixes the generated namespace, points at the local schema, and declares both
transports — HTTP for queries and mutations, WebSocket for subscriptions:

```json
{
  "schema": "schema.graphql",
  "documents": "GraphQL/**/*.graphql",
  "extensions": { "strawberryShake": {
      "name": "BlogClient",
      "namespace": "GraphQLPractice.Client.GraphQL",
      "url": "http://localhost:5100/graphql",
      "records": { "inputs": false, "entities": false },
      "transportProfiles": [ { "default": "Http", "subscription": "WebSocket" } ]
  } }
}
```

`Program.cs` binds the URL as a typed option and injects it into both transports.
`GraphqlSettings.WebSocketUrl` derives `ws://` from `http://` (and `wss://` from `https://`):

```csharp
builder.Services.AddOptions<GraphqlSettings>()
    .BindConfiguration(GraphqlSettings.SectionName)
    .Validate(s => !string.IsNullOrWhiteSpace(s.Url), "Configuration 'Graphql:Url' is not configured.")
    .ValidateOnStart();
builder.Services.AddBlogClient()
    .ConfigureHttpClient((sp, c) => c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<GraphqlSettings>>().Value.Url))
    .ConfigureWebSocketClient((sp, c) => c.Uri = new Uri(sp.GetRequiredService<IOptions<GraphqlSettings>>().Value.WebSocketUrl));
```

`wwwroot/appsettings.json` supplies `{ "Graphql": { "Url": "http://localhost:5100/graphql" } }`. When the
server schema changes, re-pull and regenerate: `dotnet graphql update -p src\GraphQLPractice.Client` (API
running), then `dotnet graphql generate -p src\GraphQLPractice.Client` — or the build fails on missing members.

## What Strawberry Shake generates

Build once and code appears under `obj/`. You never edit it:

- **`IBlogClient`** with one member per document: `GetPosts`, `GetAuthors`, `GetPostById`, `GetTags`,
  `CreatePost`, `AddComment`, `CreateAuthor`, `OnCommentAdded`, `OnPostPublished`.
- **Result interfaces** such as `IGetPostsResult` and `ICreatePostResult`.
- **Razor components** — `<UseGetPosts>`, `<UseGetAuthors>`, `<UseOnCommentAdded>` — and the
  `AddBlogClient()` DI extension used in `Program.cs`.

Because every list is a Relay connection, each document pages nested collections explicitly:

```graphql
query GetPosts($first: Int, $after: String, $where: BlogPostFilterInput, $order: [BlogPostSortInput!]) {
  posts(first: $first, after: $after, where: $where, order: $order) {
    totalCount
    nodes { id title author { id name } comments(first: 1) { totalCount } }
  }
}
```

Each nested connection has its own cap (`Author.posts` 10, `BlogPost.comments` 50, `BlogPost.tags` 20);
asking for more returns `HC0051`. The result type keeps the connection shape (`Nodes`, `TotalCount`, `PageInfo`).

## The imperative and declarative styles

`Pages/Posts.razor` is **imperative**: inject `IBlogClient`, call `ExecuteAsync`, then inspect
`IsErrorResult()`, `Errors` and `Data`:

```csharp
var result = await Client.GetPosts.ExecuteAsync(5, endCursor, where, order);
if (result.IsErrorResult())
{
    error = string.Join("; ", result.Errors.Select(e => e.Message));
    return;
}
if (result.Data?.Posts.Nodes is { } nodes) { posts.AddRange(nodes); }
```

`Pages/Authors.razor` is **declarative**: the generated component owns loading and error states and
hands the result to `ChildContent` through `context`:

```razor
<UseGetAuthors>
    <ChildContent>@foreach (var author in context.Authors!.Nodes!) { <h5>@author.Name</h5> }</ChildContent>
    <LoadingContent><p>Loading…</p></LoadingContent>
    <ErrorContent>@context.First().Message</ErrorContent>
</UseGetAuthors>
```

## Global ids round-trip through `node`

Global object identification makes every entity `id` a global `ID` string (base64 of `Type:localId`,
so `BlogPost:1` is `QmxvZ1Bvc3Q6MQ==`). `GetPostById.graphql` takes an `ID!` and narrows with a fragment:

```graphql
query GetPostById($id: ID!) {
  node(id: $id) {
    ... on BlogPost {
      id title slug body status
      author { id name }
      comments(first: 50) { totalCount nodes { id text author { id name } } }
    }
  }
}
```

`PostDetail.razor` is `@page "/posts/{Id}"`, where `Id` is a **string**, and the result is cast to the
fragment type:

```csharp
[Parameter] public string Id { get; set; } = string.Empty;
var postResult = await Client.GetPostById.ExecuteAsync(Id);
post = postResult.Data?.Node as IGetPostById_Node_BlogPost;
```

The list page links with `$"posts/{Uri.EscapeDataString(post.Id)}"`, so the `==` in the base64 id
survives the URL. The same strings flow into mutations, and the dropdowns bind `author.Id`/`tag.Id`:

```csharp
var input = new AddCommentInput { PostId = Id, AuthorId = commentAuthorId, Text = commentText };
```

## A live comment feed over WebSocket

`OnCommentAdded.graphql` is a subscription keyed by the post's global id:

```graphql
subscription OnCommentAdded($postId: ID!) {
  onCommentAdded(postId: $postId) { id text createdAt author { id name } }
}
```

`PostDetail.razor` starts it with `Watch(Id)`. New comments join the list the initial query filled,
de-duped by id — the mutation deliberately does *not* add its own comment locally, because the
subscription delivers it:

```csharp
subscription = Client.OnCommentAdded.Watch(Id).Subscribe(result =>
{
    if (result.Data?.OnCommentAdded is not { } added) { return; }
    if (AddComments([new CommentView(added.Id, added.Text, added.Author.Name, added.CreatedAt)]))
        InvokeAsync(StateHasChanged);
});
```

The subscription is disposed in `Dispose()`, so leaving the page closes the socket.

## Try it yourself

1. Start both projects, browse `http://localhost:5200/posts`, and open a post.
   ```powershell
   dotnet run --project src\GraphQLPractice.Api
   dotnet run --project src\GraphQLPractice.Client
   ```
2. Open the same post in two tabs and add a comment in one — the other updates live, no refresh.
3. Add `publishedAt` to `GraphQL/GetPosts.graphql`, then regenerate and rebuild.
   ```powershell
   dotnet graphql generate -p src\GraphQLPractice.Client
   dotnet build src\GraphQLPractice.Client
   ```
4. In Postman folder **5. Global object identification → Get IDs**, copy a post id and paste it into
   `node(id:)`; compare it to `base64("BlogPost:1")`.

## Key takeaways

- One `.graphqlrc.json` plus `.graphql` documents is the entire client API surface.
- `IBlogClient` and the `<Use…>` components are generated; never edit them.
- The options pattern injects one URL into HTTP and derives the WebSocket URL from it.
- Global `ID` strings are strings end to end: route parameter, cast target, mutation input.
- Subscriptions arrive over `graphql-transport-ws`; de-dupe by id.

## Where to go next

- [Debugging and cost](09-debugging-and-cost.md) — read the two error channels and the cost model.
- [The guided GraphQL tour](../docs/graphql-tour.md) — the client section and §15 on global ids.
- [Client README](../src/GraphQLPractice.Client/README.md) — layout and regeneration commands.
- [Postman collection](../docs/graphql-practice.postman_collection.json) — the same operations without the UI.
