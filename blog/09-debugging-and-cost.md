# Debugging and cost: reading GraphQL errors without guessing

> Part 9 of the GraphQL .NET Practice study series

GraphQL answers almost every problem with HTTP 200, which makes "just read the status code" useless.
Once you know where errors live, debugging is mechanical: a request-level failure is a 400 with no
`path`, a field failure is a 200 with `errors[].path` and an `extensions.code`, and a domain failure
is a normal payload with an `errors` array. This post covers all three channels, then the cost model
behind `HC0047` and the header that measures it.

## What you'll learn

- The three places an error can hide, and how to tell them apart.
- Why a 400 has no `path` but a 200 almost always does.
- Reading `message`, `path`, `locations`, `extensions.code` and `extensions.coordinate`.
- Why a missing object is `null` rather than an error.
- How field cost and type cost multiply across nested lists.
- Measuring any operation with the `GraphQL-Cost: report` header.

## The three channels errors travel on

There is no single "error" concept. There are three, and each needs a different reaction:

| Channel | HTTP | Shape | Examples |
|---|---|---|---|
| Request / validation | 400 | `errors[]` with no `path` | malformed JSON, unknown field, wrong value type |
| Execution / field | 200 | `errors[].path` + `extensions.code`, `data` with `null` | `HC0082`, `HC0051`, `HC0047` |
| Domain | 200 | the mutation payload's own `errors` field | `PostNotFoundError`, `SlugAlreadyInUseError` |

The third is not a GraphQL error at all — it is a successful response that happens to carry a typed
result. Always select `errors { __typename ... }` on a payload, or you will never see it.

## HTTP status is the first clue

- **400** means the *request* is wrong. Validation failed before any resolver ran, so these errors
  carry **no `path`** and no partial `data`. An unknown field, `{ posts(first: 1) { nodes { nope } } }`,
  comes back as ``The field `nope` does not exist on the type `BlogPost`.`` with code `HC0012`. Raw
  malformed JSON (`{ this is not json`) is also a 400.
- **200** means execution happened. A field error carries a `path` to the failing field, `data` may
  contain `null` at exactly that position, and `extensions.code` names the rule. `HC0082`
  ("Exactly one slicing argument must be defined.") fires when you omit `first`; `HC0051` ("The
  maximum allowed items per page were exceeded.") fires when you exceed a `MaxPageSize`; `HC0047`
  ("The maximum allowed field cost was exceeded.") fires when the estimate is over budget.

One caveat from the tour: the status can depend on `Accept`. `application/graphql-response+json`
(a raw `curl`) tends to return 400, while `application/json` returns 200. The cost and paging errors
above are field errors and come back as 200 here.

## Reading an error

Each entry in `errors` is a small object. Read it field by field:

- `message` — human-readable, safe to log.
- `path` — the field path that failed, e.g. `["posts"]`.
- `locations` — line/column in the submitted document.
- `extensions.code` — the stable Hot Chocolate rule name (`HC0012`, `HC0051`, …). Branch on this,
  never on the message text.
- `extensions.coordinate` — the schema coordinate (type and field) involved.
- `extensions.exception.stackTrace` — present in Development only.

That stack trace appears because `Program.cs` sets `IncludeExceptionDetails = true` in Development;
it is masked in Production. Debugging with stack traces is a local-only privilege.

## Missing objects are `null`, not errors

A single-object lookup that finds nothing is not a failure. Ask
`{ postById(id: 99999) { id title } }` and you get `"data": { "postById": null }` — HTTP 200, no
`errors`. This is why the client's detail query is cast from a nullable node
(`postResult.Data?.Node as IGetPostById_Node_BlogPost`) and why `PostDetail.razor` renders
"Post not found." instead of an alert. Write queries and UI that tolerate nullability; only mutations
report missing entities, and they do it as a typed `errors` entry, not a field error.

## The cost model and the 10,000 budget

Hot Chocolate estimates an operation's cost before running it and rejects anything over budget. Two
budgets apply independently: **field cost** (resolver and input work — the one that usually trips
first) and **type cost** (objects in the response).

Cost multiplies at every list nesting level, and some fields are expensive:

| Element | Weight |
|---|---|
| Scalars / enums (`id`, `title`, `totalCount`) | 0 |
| Fields returning an object | 1 |
| Fields without a pure resolver (DataLoader-backed `author`, `comments`, …) | 10 |
| A paged list | its `first` value (or `DefaultPageSize`), capped by `MaxPageSize` |

Hot Chocolate's defaults are `MaxFieldCost = 1_000` and `MaxTypeCost = 10_000`. This project raises
both to `10_000` (`ModifyCostOptions`) for one practical reason: Hot Chocolate prices a
**variable-bound** filter or sort input at its worst case. A normal call like
`posts(first: 5, where: $where)` is estimated near 2,371 for `BlogPostFilterInput` alone, even with
`where: null`, versus about 21 when the same filter is written as a literal. The larger budget lets
real client operations through while the analyzer still blocks genuinely expensive ones.

If a legitimate operation is rejected, bound it before raising the budget: use a smaller `first`,
select less, or shrink the filter input type.

## Measuring cost with `GraphQL-Cost: report`

The header turns the estimate into data instead of an error:

```
GraphQL-Cost: report     # executes and returns extensions.cost / operationCost { fieldCost, typeCost }
GraphQL-Cost: validate   # measures without executing
```

In Nitro add it under **Settings → HTTP headers**; in Postman it is set on folder
**2. Connections & paging → First page** and on **7. Errors & cost → Cost report**. Send a nested
query with that header and read `extensions.cost` in the response — no guessing, no rejection.

## Common errors and fixes

| Symptom | Meaning | Fix |
|---|---|---|
| `HC0012` / 400, unknown field | The request failed validation before execution. | Check the field name in the Schema tab; re-run introspection. |
| `HC0082`, "Exactly one slicing argument must be defined." | A connection has no `first`/`last` (`RequirePagingBoundaries` on). | Add `first: 10` (or `last`). |
| `HC0051`, "The maximum allowed items per page were exceeded." | You asked for more than a field's `MaxPageSize`. | Lower `first` to the cap (Author.posts 10, BlogPost.comments 50, BlogPost.tags 20). |
| `HC0047`, "The maximum allowed field cost was exceeded." | The estimate is over `MaxFieldCost`/`MaxTypeCost`. | Bound nested lists, select fewer fields, or raise the budget. |
| `data.postById: null`, no errors | The lookup found nothing; objects are nullable. | Handle `null` in the UI; do not expect an error. |
| `SlugAlreadyInUseError` in a payload's `errors` | Domain failure, HTTP 200. | Branch on `__typename`; surface `slug` and `message`. |
| `AuthorNotFoundError` / `PostNotFoundError` / `TagNotFoundError` | A mutation referenced a missing entity. | Read the typed error; validate ids before submitting. |

## Try it yourself

1. In Nitro, run `{ posts { nodes { id } } }` (no `first`) and read the `HC0082` error, then add
   `first: 5` and watch it succeed.
2. Add the `GraphQL-Cost: report` header, run a nested query, and read `extensions.cost`.
   ```graphql
   { posts(first: 10) { nodes { title author { name } comments(first: 10) { totalCount } } } }
   ```
3. Ask for `posts(first: 100) { nodes { id } }` to trigger `HC0051` and note `data.posts: null` in
   the 200 response.
4. Open Postman folder **7. Errors & cost** and run the four saved requests (missing boundary,
   unknown field, malformed JSON, cost report) to see all three channels side by side.
5. In Postman, run **6. Mutations → Create post** twice with the same title; the second response is a
   `SlugAlreadyInUseError` inside the payload's `errors`.
6. Open the **8. Subscriptions (WebSocket)** folder, connect over `graphql-transport-ws`
   (`connection_init` → `connection_ack` → `subscribe` → `next`), then publish a post and watch the
   `next` frame arrive.

## Key takeaways

- 400 = bad request, no `path`; 200 + `errors[].path` = field error; payload `errors` = domain error.
- Branch on `extensions.code` and `__typename`, not on message strings.
- A missing object is `null`; only mutations report missing entities.
- Cost = field cost + type cost, multiplied across nested lists.
- The 10,000 budget compensates for Hot Chocolate over-pricing variable-bound filters.
- `GraphQL-Cost: report` measures an operation instead of failing it.

## Where to go next

- [The Blazor client](08-the-blazor-client.md) — how the client renders these errors.
- [The guided GraphQL tour](../docs/graphql-tour.md) — §10 Debugging and the cost-analysis note.
- [Postman collection](../docs/graphql-practice.postman_collection.json) — folders 7 and 8 reproduce everything here.
- [Repository README](../README.md) — the troubleshooting table maps each symptom to a fix.
- [Client README](../src/GraphQLPractice.Client/README.md) — the client-side error patterns.
