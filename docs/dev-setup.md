# Development setup

## 1. Shared PostgreSQL instance (`masterTest`)

Every local project on this machine shares **one** Postgres server instead of starting a new
container per app. It is defined outside this repository:

```
D:\PersonalProjects\_infra\postgres-master-test\
├─ docker-compose.yml      # postgres:18.6-alpine, container "master-test-postgres"
├─ .env                    # POSTGRES_PASSWORD (git-ignored)
├─ new-db.ps1              # helper to create/manage databases
└─ README.md
```

| | |
|---|---|
| Image | `postgres:18.6-alpine` |
| Container | `master-test-postgres` |
| Host port | **55432** → container 5432 |
| Volume | `master-test-pgdata` mounted at `/var/lib/postgresql` (Postgres 18 layout) |
| Superuser | `postgres` / password in the infra `.env` (default `mastertest`) |

### Start it

```powershell
cd D:\PersonalProjects\_infra\postgres-master-test
docker compose up -d
docker compose ps          # wait for "healthy"
```

`restart: unless-stopped` means it comes back automatically with Docker Desktop.

### Create this app's database

One server, one database per app:

```powershell
.\new-db.ps1 graphqlpractice     # creates if missing
.\new-db.ps1 -List               # list databases
.\new-db.ps1 scratch -Drop       # drop + recreate
```

### Connection string

```
Host=localhost;Port=55432;Database=graphqlpractice;Username=postgres;Password=mastertest
```

It is stored in `src/GraphQLPractice.Api/appsettings.Development.json`, which is committed on
purpose (local-only dev credential). Override it with .NET user-secrets or the
`ConnectionStrings__Postgres` environment variable for anything else.

### Why port 55432?

A native Postgres install typically listens on `5432`. Using `55432` lets the shared container and
a local install coexist. If you have no local Postgres and prefer `5432`, change the port mapping in
the infra `docker-compose.yml` and update the connection string.

### Reusing it for a new app

1. `.\new-db.ps1 mynewapp`
2. Point the app at `Host=localhost;Port=55432;Database=mynewapp;...`

No new container, no new volume, no waiting for an image, and a single `docker compose down -v`
tears everything down when you really want to.

## 2. .NET tooling

The repo ships a local tool manifest at `dotnet-tools.json` (repo root):

```powershell
dotnet tool restore
dotnet ef --version          # 10.0.12
dotnet graphql --help        # Strawberry Shake CLI 16.6.6
```

`dotnet ef` is used for migrations; the Strawberry Shake CLI pulls the schema and regenerates the
client.

## 3. Packages and versions

Versions are centralized with **Central Package Management** in `Directory.Packages.props`.
Project files reference packages without a `Version`. Shared compiler settings live in
`Directory.Build.props` (`net10.0`, nullable, implicit usings).

All Hot Chocolate and Strawberry Shake packages must share the same version — currently `16.6.6`.

## 4. Code style & analyzers

- **StyleCop.Analyzers** `1.2.0-beta.556` is referenced once in `Directory.Build.props`, so every
  project (including future ones) gets it. The build is kept at zero warnings.
- **`stylecop.json`** (repo root) configures the analyzer: no file headers, no XML-doc
  requirements, file-scoped-friendly using placement (`outsideNamespace`), leading underscores
  allowed. It is wired up as an `AdditionalFiles` item in `Directory.Build.props`.
- **`.editorconfig`** (repo root) owns rule *severities*. Table of what is enforced vs relaxed:

  | Rule | Setting | Why |
  |---|---|---|
  | Naming (`SA13xx`) | warning | Enforced |
  | Ordering (`SA1201`–`SA1208`) | warning | Enforced |
  | One type per file (`SA1402`), file name matches type (`SA1649`) | warning | Enforced |
  | Using placement (`SA1200`) | warning | Enforced (`outsideNamespace`) |
  | Blank lines between members (`SA1516`), trailing commas (`SA1413`) | warning | Enforced |
  | File headers (`SA1633`–`SA1638`), `this.` prefix (`SA1101`), `SA0001` | none | Not this project's style |
  | Closing-paren layout (`SA1009`, `SA1111`) | none | Owned by the C# formatter (see below) |
  | `IDE0161` under `Data/Migrations/` | none | EF Core migrations are generated |

- **Why `SA1009`/`SA1111` are off:** the C# formatter used in this repo places the closing parenthesis
  of a multi-line argument/parameter list on its own line, which is the opposite of what those two
  rules require — they cannot both be satisfied. Since the formatter rewrites the files anyway, it
  wins. If you would rather have StyleCop own formatting, switch the formatter (e.g. use
  `dotnet format` instead of a CSharpier-style formatter) and re-enable the two rules.
- **Generated migrations** are excluded with `generated_code = true`, so no analyzer or formatter
  touches `Data/Migrations/`.

- To silence or promote a rule, add `dotnet_diagnostic.SAxxxx.severity = none|suggestion|warning|error`
  under `[*.cs]` in `.editorconfig`.

## 5. Running

```powershell
dotnet run --project src\GraphQLPractice.Api      # http://localhost:5100
dotnet run --project src\GraphQLPractice.Client   # http://localhost:5200
```

Both ports are fixed in `Properties/launchSettings.json`.

## 6. Stopping

Stop the API/client by closing their terminals (`Ctrl+C`). Stop Postgres with
`docker compose stop` in the infra folder (data is preserved).
