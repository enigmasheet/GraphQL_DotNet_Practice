# GraphQL .NET Practice

A small fullstack GraphQL playground for learning how GraphQL works end to end:

| Layer | Technology |
|---|---|
| GraphQL server | [Hot Chocolate](https://chillicream.com/docs/hotchocolate) 16 (code-first) |
| Database | PostgreSQL 18 in a shared Docker instance, via EF Core 10 + Npgsql |
| Client | Blazor WebAssembly (.NET 10) with [Strawberry Shake](https://chillicream.com/docs/strawberryshake) |
| Code style | [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) + `.editorconfig` (build stays warning-free) |
| Concepts covered | queries, nested resolvers, paging, filtering, sorting, projections, DataLoaders (N+1), mutations with typed errors, subscriptions over WebSocket |

The domain is a tiny blog: **Author → BlogPost → Comment**, with **Tag** a many-to-many on posts.

> New to the project? Read **[docs/dev-setup.md](docs/dev-setup.md)** first, then work through
> **[docs/graphql-tour.md](docs/graphql-tour.md)**.

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
src/
  GraphQLPractice.Api/                # Hot Chocolate server + EF Core
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

```graphql
type Query {
  authors(first: Int, after: String, where: AuthorFilterInput, order: [AuthorSortInput!]): AuthorsConnection
  posts(first: Int, after: String, where: BlogPostFilterInput, order: [BlogPostSortInput!]): PostsConnection
  authorById(id: Int!): Author
  postById(id: Int!): BlogPost
  tags: [Tag!]!
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
  onCommentAdded(postId: Int!): Comment!
}
```

Fetch the full schema at `http://localhost:5100/graphql/schema`, or with:

```powershell
dotnet run --project src\GraphQLPractice.Api -- schema export
```

## Feature → file map

| Concept | Where to look |
|---|---|
| Query root, paging/filtering/sorting/projection | `src/GraphQLPractice.Api/GraphQL/Query.cs` |
| Mutations + typed errors (`[Error(typeof(...))]`) | `src/GraphQLPractice.Api/GraphQL/Mutation.cs` |
| Subscriptions + dynamic topics | `src/GraphQLPractice.Api/GraphQL/Subscription.cs` |
| Node resolvers (nested fields) | `src/GraphQLPractice.Api/GraphQL/Types/*.cs` |
| DataLoaders (batch + group) | `src/GraphQLPractice.Api/GraphQL/DataLoaders/*.cs` |
| Domain model + EF mapping | `src/GraphQLPractice.Api/Models`, `Data/AppDbContext.cs` |
| Seed data | `src/GraphQLPractice.Api/Data/SeedData.cs` |
| Server wiring (DI, CORS, subscriptions) | `src/GraphQLPractice.Api/Program.cs` |
| Client operations | `src/GraphQLPractice.Client/GraphQL/*.graphql` |
| Client pages | `src/GraphQLPractice.Client/Pages/*.razor` |
| Client wiring (client + transports) | `src/GraphQLPractice.Client/Program.cs` |

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
| Client debug profile looks stale | Delete `src\GraphQLPractice.Client\GraphQLPractice.Client.csproj.user` (it pins an old `https` profile). It is git-ignored. |

## A note on "Scalar" vs "scalar"

[Scalar](https://scalar.com) is an API reference UI for **OpenAPI/AsyncAPI** documents — it cannot
render a GraphQL schema. The GraphQL equivalent of Swagger UI is **Nitro**, which Hot Chocolate
serves at `/graphql`. Separately, a GraphQL **scalar** is a leaf type (`String`, `Int`, `DateTime`,
or a custom one); those are configured in code, not in the UI. See
[docs/graphql-tour.md](docs/graphql-tour.md#scalars).

## Deliberately out of scope

Auth/authorization, persisted queries, cost analysis, schema stitching/Fusion, NodaTime scalars,
tests, and CI. Each is a natural next step; see the Hot Chocolate docs for guidance.
