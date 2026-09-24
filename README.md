# GraphQL .NET Practice

A small fullstack GraphQL playground for learning how GraphQL works end to end:

| Layer | Technology |
|---|---|
| GraphQL server | [Hot Chocolate](https://chillicream.com/docs/hotchocolate) 16 (code-first) |
| Architecture | Modular monolith — feature modules under `Modules/{Authors,Posts,Comments,Tags}` |
| Database | PostgreSQL 18 in a shared Docker instance, via EF Core 10 + Npgsql |
| Client | Blazor WebAssembly (.NET 10) with [Strawberry Shake](https://chillicream.com/docs/strawberryshake) |
| Code style | [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) + `.editorconfig` (build stays warning-free) |
| Concepts covered | queries, nested resolvers, paging, filtering, sorting, projections, global object identification (Relay `Node`), DataLoaders (N+1), mutations with typed errors, subscriptions over WebSocket |

The domain is a tiny blog: **Author → BlogPost → Comment**, with **Tag** a many-to-many on posts.

> New to the project? Read **[docs/dev-setup.md](docs/dev-setup.md)** first, then work through
> **[docs/graphql-tour.md](docs/graphql-tour.md)** and the
> **[query variations cookbook](docs/query-variations.md)**.

## Repository layout

```
GraphQL_DotNet_Practice.slnx          # solution (.slnx is the .NET 10 default)
Directory.Build.props                 # shared MSBuild settings + StyleCop for every project
Directory.Packages.props              # Central Package Management (all versions live here)
stylecop.json                         # StyleCop.Analyzers settings
.editorconfig                         # editor settings + analyzer severities
dotnet-tools.json                     # local tools: dotnet-ef, Strawberry Shake CLI
docs/
  dev-setup.md                        # environment + shared database setup
  graphql-tour.md                     # guided GraphQL feature tour
  query-variations.md                 # cookbook: the same query many ways
  graphql-practice.postman_collection.json  # importable Postman collection (introspection, paging,
                                      #   filtering, Node, mutations with id-capture scripts, WS subs)
src/
  GraphQLPractice.Api/                # Hot Chocolate server + EF Core
    Modules/                          # Authors | Posts | Comments | Tags (modular monolith)
  GraphQLPractice.Client/             # Blazor WASM + Strawberry Shake client
```

> The shared PostgreSQL instance lives **outside this repo**, in
> `D:\PersonalProjects\_infra\postgres-master-test`. See its own README for details.

## Prerequisites

- .NET SDK 10
- Docker Desktop (for the shared `masterTest` Postgres)
- Optional: a browser for the Nitro IDE

## Quick start

```powershell
# 1. Start the shared Postgres instance (once per machine boot)
cd D:\PersonalProjects\_infra\postgres-master-test
docker compose up -d
.\new-db.ps1 graphqlpractice        # creates the database for this app

# 2. Restore local tools and packages
cd D:\PersonalProjects\GraphQL_DotNet_Practice
dotnet tool restore
dotnet restore

# 3. Apply migrations (also seeds sample data on first run in Development)
dotnet ef database update --project src\GraphQLPractice.Api

# 4. Run the API  ->  http://localhost:5100/graphql  (Nitro IDE)
dotnet run --project src\GraphQLPractice.Api

# 5. In a second terminal, run the client  ->  http://localhost:5200
dotnet run --project src\GraphQLPractice.Client
```

The API also serves:

- `http://localhost:5100/graphql` — GraphQL endpoint **and** the Nitro IDE when opened in a browser
- `http://localhost:5100/graphql/ui` — the IDE on its own path
- `http://localhost:5100/graphql/schema` — the schema SDL

You can skip step 5 entirely and explore the API in Nitro.

## Ports

| Service | URL |
|---|---|
| GraphQL API | `http://localhost:5100` |
| Blazor client | `http://localhost:5200` |
| Postgres (shared) | `localhost:55432` |

Ports are fixed in each project's `Properties/launchSettings.json`. `55432` is used because a
local Postgres often occupies `5432`; see [docs/dev-setup.md](docs/dev-setup.md).

## The GraphQL schema at a glance

Every list is a Relay connection built with `[UseConnection]`, and `RequirePagingBoundaries` is on —
so every paged field takes `first` (or `last`), plus optional `where`/`order`.

Global object identification is enabled, so every entity `id` is a global `ID!` and any node can be
fetched with `node(id: ID!)` (or many at once with `nodes(ids: [ID!]!)`). Mutations take global ids in
their inputs too.

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

type Author implements Node {
  id: ID!
  name: String!
  bio: String
  createdAt: DateTime!
  posts(first: Int, after: String, where: BlogPostFilterInput, order: [BlogPostSortInput!]): BlogPostConnection!
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

type Comment implements Node {
  id: ID!
  text: String!
  createdAt: DateTime!
  author: Author!
}

type Tag implements Node {
  id: ID!
  name: String!
}

type Mutation {
  createAuthor(input: CreateAuthorInput!): CreateAuthorPayload!
  createPost(input: CreatePostInput!): CreatePostPayload!
  updatePost(input: UpdatePostInput!): UpdatePostPayload!
  deletePost(input: DeletePostInput!): DeletePostPayload!
  addComment(input: AddCommentInput!): AddCommentPayload!
}

type Subscription {
  onPostPublished: BlogPost!
  onCommentAdded(postId: ID!): Comment!
}
```

Fetch the full schema at `http://localhost:5100/graphql/schema`, or with:

```powershell
dotnet run --project src\GraphQLPractice.Api -- schema export
```

## Feature → file map

| Concept | Where to look |
|---|---|
| Module pattern (`IModule`, registration) | `src/GraphQLPractice.Api/Modules/IModule.cs`, `ModuleRegistry.cs` |
| Query root + connections, filtering/sorting/projection | `src/GraphQLPractice.Api/Modules/{Posts,Authors,Tags}/*Queries.cs` |
| Mutations + typed errors (`[Error(typeof(...))]`) | `src/GraphQLPractice.Api/Modules/{Authors,Posts,Comments,Tags}/*Mutations.cs` |
| Subscriptions + dynamic topics | `Modules/Posts/PostSubscription.cs`, `Modules/Comments/CommentSubscription.cs` |
| Node resolvers (nested fields) | `Modules/*/*Node.cs` |
| DataLoaders (batch) | `Modules/Authors/DataLoaders`, `Modules/Posts/DataLoaders` |
| Global object identification (`Node`, `[Node(...)]`, `[ID]`) | `Modules/*/*NodeResolver.cs`, `Modules/*/*Mutations.cs` |
| Domain model | `src/GraphQLPractice.Api/Models` |
| Per-module EF configuration | `Modules/*/*Configuration.cs` (applied by `Data/AppDbContext.cs`) |
| Seed data | `src/GraphQLPractice.Api/Data/SeedData.cs` |
| Server wiring (modules, GraphQL, CORS) | `src/GraphQLPractice.Api/Program.cs` |
| Client operations | `src/GraphQLPractice.Client/GraphQL/*.graphql` |
| Client pages | `src/GraphQLPractice.Client/Pages/*.razor` |
| Client wiring (client + transports) | `src/GraphQLPractice.Client/Program.cs` |

## Explore with Postman

Import `docs/graphql-practice.postman_collection.json` (**Import → File**). It runs against
`http://localhost:5100/graphql` via the `baseUrl` collection variable and is grouped into
introspection, paging, filtering/sorting, nested resolvers, global object identification (Node),
mutations, errors/cost, and WebSocket subscriptions.

Run **5. Global object identification → Get IDs** first: its test script captures the opaque global
ids into the `postId`/`authorId`/`tagId` collection variables, and the mutation requests capture the
ids they create — so **Update post**, **Delete post**, **Add comment** and **Delete tag** chain without
copy-paste. Subscriptions use a Postman **WebSocket** request (see that folder's description for the
`graphql-transport-ws` frames).

## Common commands

```powershell
dotnet build                                   # build both projects
dotnet ef migrations add <Name> --project src\GraphQLPractice.Api --output-dir Data\Migrations
dotnet ef database update --project src\GraphQLPractice.Api
dotnet run --project src\GraphQLPractice.Api -- schema export
dotnet graphql update -p src\GraphQLPractice.Client   # re-pull schema (API must be running)
dotnet graphql generate -p src\GraphQLPractice.Client # regenerate the typed client
```

## Watch the SQL (this is the fun part)

`appsettings.Development.json` sets `Microsoft.EntityFrameworkCore.Database.Command` to
`Information`, so the API console prints every SQL statement. Try this to *see* N+1 being solved:

1. In Nitro, run `{ posts(first: 5) { nodes { title author { name } } } }`.
2. Look at the console: one query for the posts, then **one** batched query for all authors
   (`WHERE a."Id" = ANY (@ids)`) — not five.
3. Remove the `author` field and run again; only the posts query appears.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `MSB3061: Unable to delete file ... is denied` | A previous API instance is still running and locking `bin`. Stop it (close the terminal / `Get-Process GraphQLPractice.Api \| Stop-Process`) then rebuild. |
| `55000`/connection refused to Postgres | The shared instance is not running: `docker compose up -d` in `_infra\postgres-master-test`. |
| `database "graphqlpractice" does not exist` | Run `.\new-db.ps1 graphqlpractice` in the infra folder. |
| Client shows no data / CORS errors | The API must be running on `5100` and must allow `http://localhost:5200` (see `Cors:AllowedOrigins` in `appsettings.json`). |
| Nitro page is blank | It is loaded from a CDN; check your connection or set `ServeMode = Embedded`. |
| Nitro shows **"Fusion Operation Plan Not Supported"** | Expected. Query plans belong to a Fusion gateway (`AddGraphQLGateway()`); this is a single graph, so there is no plan. Nothing to fix — see [docs/graphql-tour.md](docs/graphql-tour.md#using-nitro). |
| Variables look ignored / filter returns nothing | Omitting a variable is not the same as sending `null`; `eq: null` matches nothing. Also check types (`Int` vs `String`, enums as `"PUBLISHED"`). See [docs/graphql-tour.md](docs/graphql-tour.md#2-variables). |
| A query returns `null` where you expected an error | Missing objects are `null`, not errors (e.g. `postById(id: 99999)`). Domain errors only appear on mutation payloads. See [docs/graphql-tour.md](docs/graphql-tour.md#10-debugging). |
| Can't tell why a request failed | `400` = bad request (no `path`); `200` + `errors[].path` = field error. Read `extensions.code` (e.g. `HC0012`, `HC0051`). See [docs/graphql-tour.md](docs/graphql-tour.md#10-debugging). |
| `HC0082: Exactly one slicing argument must be defined` | Every paged field requires `first` (or `last`). Add it — e.g. `posts(first: 10)`. |
| `HC0047: The maximum allowed field cost was exceeded` | The operation is too expensive (cost multiplies across nested lists). Bound it with smaller `first`, select less, or raise the budget — this project sets `MaxFieldCost`/`MaxTypeCost` to 10,000 because Hot Chocolate over-prices variable-bound filters. Measure with the `GraphQL-Cost: report` header — see [docs/graphql-tour.md](docs/graphql-tour.md#cost-analysis-hc0047). |
| Client debug profile looks stale | Delete `src\GraphQLPractice.Client\GraphQLPractice.Client.csproj.user` (it pins an old `https` profile). It is git-ignored. |

## Deliberately out of scope

Auth/authorization, persisted queries, schema stitching/Fusion, NodaTime scalars, tests, and CI. Each
is a natural next step; see the Hot Chocolate docs for guidance.
