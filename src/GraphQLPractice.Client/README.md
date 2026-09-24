# GraphQLPractice.Client

A Blazor WebAssembly client for the API, using **Strawberry Shake** to generate a typed C# GraphQL
client from `.graphql` documents at build time.

## Running

The API must be running first.

```powershell
dotnet run --project src\GraphQLPractice.Api      # http://localhost:5100
dotnet run --project src\GraphQLPractice.Client   # http://localhost:5200
```

The GraphQL URL is read from `wwwroot/appsettings.json`:

```json
{ "GraphqlUrl": "http://localhost:5100/graphql" }
```

## Layout

```
.graphqlrc.json             # Strawberry Shake config (namespace, transports, schema, documents)
schema.graphql              # schema pulled from the running API (committed)
schema.extensions.graphql   # client-side schema extensions (committed)
GraphQL/*.graphql           # operations (queries, mutations, subscriptions)
Program.cs                  # registers the client + HTTP/WebSocket transports
Pages/                      # Posts, PostDetail, CreatePost, Authors, Home
Layout/                     # NavMenu, MainLayout
```

## Generated client

Build once and Strawberry Shake generates (into `obj/`):

- `IBlogClient` with `GetPosts`, `GetAuthors`, `GetPostById`, `GetTags`, `CreatePost`,
  `AddComment`, `CreateAuthor`, `OnCommentAdded`, `OnPostPublished`.
- Result interfaces such as `IGetPostsResult`, `ICreatePostResult`.
- Razor components (`UseGetPosts`, `UseGetAuthors`, `UseOnCommentAdded`, …).
- The `AddBlogClient()` DI extension.

> **Every list on the server is a connection**, so operations page nested collections explicitly:
> `comments(first: 50) { totalCount nodes { … } }`, `tags(first: 10) { nodes { … } }`. If the server
> schema changes, re-pull it (see *Regenerating* below) so the generated `Nodes`/`TotalCount` members
> line up — otherwise the build fails with missing members.

The result types expose the connection shape: `result.Data.Posts.Nodes`, `.TotalCount`, `.PageInfo`.

Two usage styles appear in `Pages/`:

- **Declarative** (`Authors.razor`) — the generated component handles loading/error:

  ```razor
  <UseGetAuthors>
      <ChildContent>@foreach (var a in context.Authors!.Nodes!) { ... }</ChildContent>
      <LoadingContent><p>Loading…</p></LoadingContent>
      <ErrorContent>@context.First().Message</ErrorContent>
  </UseGetAuthors>
  ```

- **Imperative** (`Posts.razor`, `PostDetail.razor`, `CreatePost.razor`):

  ```csharp
  var result = await Client.GetPosts.ExecuteAsync(first, after, where, order);
  if (result.IsErrorResult()) { /* result.Errors */ }
  var connection = result.Data?.Posts;
  ```

## Regenerating after a server schema change

With the API running:

```powershell
dotnet graphql update -p src\GraphQLPractice.Client     # re-download schema.graphql
dotnet build src\GraphQLPractice.Client                 # regenerate the typed client
```

`dotnet graphql init <url> -n BlogClient -p <dir>` recreates `.graphqlrc.json` from scratch if
needed. If the API is not reachable, export the schema first and import it from a file:

```powershell
dotnet run --project src\GraphQLPractice.Api -- schema export
dotnet graphql init <dir> -n BlogClient -p src\GraphQLPractice.Client -f schema.graphqls
```

> In Visual Studio, `.graphql` files may need their **Build Action** set to **GraphQL compiler**.
> The SDK/dotnet CLI builds pick them up automatically from `.graphqlrc.json`.

## Subscriptions

`Program.cs` configures both transports:

```csharp
builder.Services
    .AddBlogClient()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(graphqlUrl))
    .ConfigureWebSocketClient(client => client.Uri = new Uri(graphqlWsUrl));
```

`OnCommentAdded.Watch(postId)` in `PostDetail.razor` receives comments live over `ws://`.
