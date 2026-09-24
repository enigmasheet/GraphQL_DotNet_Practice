# Local setup: get the whole stack running

> Part 0 of the GraphQL .NET Practice study series

You have cloned the repo and you want to see it actually work. This post takes you from a cold machine to a live GraphQL endpoint and a Blazor client that reads from it. There is no GraphQL theory here — it is the "make it run" checklist, with the reason behind each step so the next run is muscle memory. Follow it once and you have a repeatable loop for every later post.

## What you'll learn

- How to start the shared PostgreSQL instance and create this app's database
- How to restore the local .NET tools and NuGet packages
- How to apply EF Core migrations (which also seed sample data)
- How to run the API, where Nitro lives, and how to run the Blazor client
- Why the database uses port `55432`
- How to stop everything and watch the SQL the API executes

## 1. Start the shared PostgreSQL instance

Every project on this machine shares **one** Postgres server that lives outside this repository, in `D:\PersonalProjects\_infra\postgres-master-test`. Start it once per boot, then create a database for this app:

```powershell
cd D:\PersonalProjects\_infra\postgres-master-test
docker compose up -d
docker compose ps                      # wait until it reports "healthy"
.\new-db.ps1 graphqlpractice           # creates the database if missing
```

The container is `master-test-postgres` (`postgres:18.6-alpine`) and it restarts automatically with Docker Desktop. The full setup is documented in [dev-setup.md](../docs/dev-setup.md).

## 2. Restore tools and packages

The repo pins its tooling in `dotnet-tools.json`, so restore the local tools before anything else, then restore packages:

```powershell
cd D:\PersonalProjects\GraphQL_DotNet_Practice
dotnet tool restore
dotnet restore
```

You now have `dotnet ef` (for migrations) and the Strawberry Shake CLI (`dotnet graphql`) available locally. NuGet versions are centralized in `Directory.Packages.props`, so every project shares one version per package — no chasing mismatches.

## 3. Apply migrations

The API applies migrations at startup in Development, but it is cleaner to do it explicitly once so you can see the schema build:

```powershell
dotnet ef database update --project src\GraphQLPractice.Api
```

This runs the EF Core migrations and, on a fresh database, seeds sample data (`Data/SeedData.cs`). `AppDbContextFactory` supplies the connection string at design time, so the CLI does not boot the web app.

## 4. Run the API

```powershell
dotnet run --project src\GraphQLPractice.Api
```

The server serves three surfaces:

| Surface | URL |
|---|---|
| GraphQL endpoint + Nitro IDE (in a browser) | `http://localhost:5100/graphql` |
| Nitro on its own path | `http://localhost:5100/graphql/ui` |
| Schema SDL | `http://localhost:5100/graphql/schema` |

Open `http://localhost:5100/graphql`, click **Create Document**, confirm the endpoint, and click **Apply**. When the bottom-right says *Schema available*, you are connected. You can do the entire study series from Nitro and never start the client at all.

## 5. Run the client

In a second terminal:

```powershell
dotnet run --project src\GraphQLPractice.Client
```

That serves the Blazor WebAssembly app at `http://localhost:5200`. It reads the API URL from `wwwroot/appsettings.json` (`Graphql:Url`), which `Program.cs` binds as a strongly-typed option and uses to configure the HTTP and WebSocket transports. The client only works while the API on `5100` is running, because CORS is configured to allow `http://localhost:5200`.

## Why port 55432?

A native Postgres install usually listens on `5432`. The shared container maps host port **`55432`** to the container's `5432`, so the container and any local install can coexist without a fight. That is why the connection string in `appsettings.Development.json` reads `Port=55432`. The password `mastertest` is committed on purpose — it is a local-only dev credential.

## Stop the services

- API and client: `Ctrl+C` in each terminal (or close the window).
- Postgres: run `docker compose stop` in the infra folder. The data volume is preserved, so the next `docker compose up -d` brings your rows back.

## How to check your SQL

`appsettings.Development.json` sets `Microsoft.EntityFrameworkCore.Database.Command` to `Information`, so the API console prints **every SQL statement**. This is the single most useful debugging tool in the project. Run a query in Nitro and watch the console; when a later post talks about N+1, you will literally see one batched query instead of many.

## Try it yourself

With the API running, paste this into Nitro's Request pane and put the variables in the Variables pane.

```graphql
query FirstPosts($first: Int!) {
  posts(first: $first) {
    totalCount
    nodes {
      id
      title
      status
      author {
        name
      }
    }
  }
}
```

```json
{ "first": 3 }
```

1. Run it. You should get three posts, a `totalCount`, and each post's author name.
2. Look at the API console: you will see one query for the posts and one batched query for the authors.
3. Try removing `author { name }` and run again — the author query disappears. You are seeing DataLoaders at work already.
4. Open `http://localhost:5100/graphql/schema` in a new tab and scroll the SDL. That file is the contract this whole series teaches you to read.

Prefer a GUI client? Import the collection at [`../docs/graphql-practice.postman_collection.json`](../docs/graphql-practice.postman_collection.json) (**Import → File**). It targets `http://localhost:5100/graphql` via its `baseUrl` variable and includes introspection, paging, Node, mutations with id-capture scripts, and WebSocket subscriptions.

## Key takeaways

- One start-of-session sequence: `docker compose up -d` → `new-db.ps1` → `dotnet tool restore` → `dotnet restore` → `dotnet ef database update`.
- The API is `http://localhost:5100`, the client is `http://localhost:5200`, Postgres is on `55432`.
- Nitro at `/graphql` (or `/graphql/ui`) is enough to explore the entire API.
- The API console prints the SQL for every request — get in the habit of watching it.

## Where to go next

- Understand what you just started: [Post 01 — Why GraphQL and the schema](01-why-graphql-and-the-schema.md)
- Deeper environment detail: [dev-setup.md](../docs/dev-setup.md)
- The full guided feature tour: [graphql-tour.md](../docs/graphql-tour.md)
- Repo overview and feature map: [README.md](../README.md)
- Server internals: [GraphQLPractice.Api README](../src/GraphQLPractice.Api/README.md)
