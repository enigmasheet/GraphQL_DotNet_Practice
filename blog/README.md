# The GraphQL .NET Practice study series

A blog-style walkthrough of this repo, written to be **read**, not just referenced. Each post is
self-contained: it explains the idea, shows real queries and code from this project, gives you a set of
things to run yourself, and ends with takeaways and a link to the next part.

The terse reference material still lives in [`docs/`](../docs) — these posts are the guided tour that
walks you through it in order.

## Before you start

You need the stack running to follow along:

- .NET SDK 10 and Docker Desktop
- the shared Postgres instance, the API on `http://localhost:5100/graphql` (Nitro IDE), and the Blazor
  client on `http://localhost:5200`

Part 00 sets all of that up. Everything else assumes the API is up.

## The series

| # | Post | What you'll practise |
|---|---|---|
| 00 | [Local setup: get the whole stack running](00-local-setup.md) | Postgres, migrations, running the API and client, watching the SQL |
| 01 | [Why GraphQL, and how to read the schema](01-why-graphql-and-the-schema.md) | the domain, the stack, introspection, the schema at a glance |
| 02 | [Querying in depth: shape, variables, and directives](02-querying-in-depth.md) | fields, aliases, variables, nesting, fragments, `@include`/`@skip` |
| 03 | [Paging, filtering, and sorting: connections end to end](03-paging-filtering-sorting.md) | Relay connections, cursors, `where`/`order`, page-size caps |
| 04 | [Mutations and typed errors: commands that read like queries](04-mutations-and-typed-errors.md) | `input` conventions, payload errors, `[Error]`, global-id inputs |
| 05 | [Subscriptions and realtime: pushing comments over WebSocket](05-subscriptions-and-realtime.md) | `onPostPublished`, dynamic topics, `graphql-transport-ws` |
| 06 | [DataLoaders and the N+1 problem](06-dataloaders-and-the-n-plus-1-problem.md) | batching, hidden navigations, seeing one batched SQL query |
| 07 | [Global object identification](07-global-object-identification.md) | the `Node` interface, `node(id:)`, `[ID]`, the node-resolver pitfall |
| 08 | [The Blazor client: a typed GraphQL client from `.graphql` files](08-the-blazor-client.md) | Strawberry Shake codegen, generated components, global-id round-trip |
| 09 | [Debugging and cost: reading GraphQL errors without guessing](09-debugging-and-cost.md) | the error channels, `extensions.code`, the cost model, `GraphQL-Cost` |

## How to read it

1. Read a post top to bottom.
2. Run its **Try it yourself** section — either in Nitro (`http://localhost:5100/graphql`) or by
   importing [`docs/graphql-practice.postman_collection.json`](../docs/graphql-practice.postman_collection.json)
   into Postman. The collection's mutation requests capture returned global ids so they chain without
   copy-paste.
3. Skim **Key takeaways**, then follow **Where to go next**.

## Suggested paths

- **New to GraphQL** — 00 → 01 → 02 → 03 → 04 → 05, then 08 for the client.
- **Server internals** — 06 (batching) → 07 (identity) → 09 (limits/debugging).
- **Client focus** — 02–05 first, then 08.
- **Just want it running** — 00.

## Companion resources

- Project overview: [`../README.md`](../README.md)
- Guided reference tour: [`../docs/graphql-tour.md`](../docs/graphql-tour.md)
- Query cookbook: [`../docs/query-variations.md`](../docs/query-variations.md)
- Environment setup details: [`../docs/dev-setup.md`](../docs/dev-setup.md)
- Postman collection: [`../docs/graphql-practice.postman_collection.json`](../docs/graphql-practice.postman_collection.json)
- Server notes: [`../src/GraphQLPractice.Api/README.md`](../src/GraphQLPractice.Api/README.md)
- Client notes: [`../src/GraphQLPractice.Client/README.md`](../src/GraphQLPractice.Client/README.md)

## Accuracy

Written against Hot Chocolate 16.6.6, Strawberry Shake 16.6.6, .NET 10, EF Core 10 and PostgreSQL 18.
If the schema changes, re-pull the client schema (`dotnet graphql update`) and treat the exported SDL
(`http://localhost:5100/graphql/schema`) as the source of truth.
