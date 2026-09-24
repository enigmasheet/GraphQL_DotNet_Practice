# Subscriptions and realtime: pushing comments over WebSocket

> Part 5 of the GraphQL .NET Practice study series

A query is a request that closes: you ask, the server answers once, the connection is done. A subscription is a request that stays open, and the server keeps pushing a new result every time something happens. This repo has exactly two subscriptions — one global, one per post — and they are the cleanest way to see how a topic string connects a write to its listeners. You will learn the schema shape, the dynamic-topic trick that routes comments to the right post, the `graphql-transport-ws` frames on the wire, and how the Blazor client consumes the stream.

## What you'll learn

- What a subscription is and how it differs from a query
- The two subscriptions: `onPostPublished` and `onCommentAdded(postId: ID!)`
- Static vs dynamic topics, and why `OnCommentAdded_{postId}` uses the decoded local id
- The publish side: `ITopicEventSender` in `createPost` and `addComment`
- The `graphql-transport-ws` frames (`connection_init`, `connection_ack`, `subscribe`, `next`)
- Consuming a subscription from Strawberry Shake, and testing with Nitro or Postman

## A subscription is an operation that stays open

Queries and mutations are one-shot HTTP requests. A subscription is resolved once per event and the connection stays open, so the transport is a WebSocket, not a single POST. Everything else is familiar: you write a selection set and choose exactly which fields of each event you receive.

```graphql
type Subscription {
  onPostPublished: BlogPost!
  onCommentAdded(postId: ID!): Comment!
}
```

Note the trailing `!` on both fields: every delivered event is a non-null entity.

## Two subscriptions: one static topic, one dynamic

`onPostPublished` is a single global stream. Its resolver in `Modules/Posts/PostSubscription.cs` is tiny — the `[EventMessage]` parameter is the published value, and the method returns it:

```csharp
[SubscriptionType]
public static partial class PostSubscription
{
    [Subscribe]
    [Topic("OnPostPublished")]
    public static BlogPost OnPostPublished([EventMessage] BlogPost post) => post;
}
```

`onCommentAdded` filters by post. `Modules/Comments/CommentSubscription.cs` declares a **dynamic** topic:

```csharp
[Subscribe]
[Topic("OnCommentAdded_{postId}")]
public static Comment OnCommentAdded(
    [ID(nameof(BlogPost))] int postId,
    [EventMessage] Comment comment) => comment;
```

The argument is `ID!` in the schema but `int` in C#: `[ID]` accepts the opaque global id and decodes it. `[Topic("OnCommentAdded_{postId}")]` is a format string, and Hot Chocolate substitutes the **decoded local id** — `{postId}` becomes `2`, not `QmxvZ1Bvc3Q6Mg==`. The effective topic is `OnCommentAdded_2`, which is exactly what the `addComment` mutation publishes to.

## The publish side

Writes publish through `ITopicEventSender`, injected into the mutation method. `createPost` publishes only when the new post is published:

```csharp
if (post.Status == PostStatus.Published)
{
    await sender.SendAsync(nameof(PostSubscription.OnPostPublished), post, ct);
}
```

`addComment` publishes to the dynamic topic, built the same way the subscription builds it:

```csharp
await sender.SendAsync($"OnCommentAdded_{postId}", comment, ct);
```

The publisher's topic string and the subscriber's computed topic must match exactly. Because both sides agree on the decoded local id, a subscriber for post 2 hears only post 2. `AddInMemorySubscriptions()` in `Program.cs` supplies the in-process event bus; being in-memory, events do not cross server instances — perfect for one process, a reason to swap in a distributed transport when you scale out.

## The wire: graphql-transport-ws

`Program.cs` calls `app.UseWebSockets()` before `MapGraphQL()`, so the same `/graphql` endpoint upgrades. Nitro and Strawberry Shake negotiate the `graphql-transport-ws` subprotocol and exchange framed JSON messages. A minimal session looks like this.

Initialize, then wait for the acknowledgement:

```json
{ "type": "connection_init", "payload": {} }
```

```json
{ "type": "connection_ack" }
```

Subscribe. Every operation gets an `id` you choose, and `payload` carries a normal GraphQL request:

```json
{ "id": "1", "type": "subscribe", "payload": { "query": "subscription OnPostPublished { onPostPublished { id title slug status author { name } } }" } }
```

Each event is a `next` frame with the same id:

```json
{ "id": "1", "type": "next", "payload": { "data": { "onPostPublished": { "id": "QmxvZ1Bvc3Q6Mg==", "title": "Realtime!" } } } }
```

The per-post stream passes the global id as a variable:

```json
{ "id": "2", "type": "subscribe", "payload": { "query": "subscription OnCommentAdded($postId: ID!) { onCommentAdded(postId: $postId) { id text author { name } } }", "variables": { "postId": "QmxvZ1Bvc3Q6Mg==" } } }
```

There are more frames (`ping`/`pong`, `complete`, `error`); these are the ones a simple session shows.

## The client side

`GraphQL/OnCommentAdded.graphql` mirrors that operation, and the Strawberry Shake config declares a WebSocket transport for subscriptions while queries and mutations stay on HTTP. `PostDetail.razor` starts the stream after loading the post and de-dupes events by comment id:

```csharp
subscription = Client.OnCommentAdded
    .Watch(Id)
    .Subscribe(result =>
    {
        if (result.Data?.OnCommentAdded is not { } added)
        {
            return;
        }

        if (AddComments([new CommentView(added.Id, added.Text, added.Author.Name, added.CreatedAt)]))
        {
            InvokeAsync(StateHasChanged);
        }
    });
```

`Watch(...)` hands you the event stream as an observable you can `Subscribe` to; the same stream can be consumed as an `IAsyncEnumerable` if you prefer `await foreach`. The page disposes the subscription when it leaves, closing the socket. De-duplication matters because the initial query and a just-posted comment can both deliver the same id.

## Testing without the client

- **Nitro** switches to a WebSocket automatically when you run a subscription, and shows a connection state instead of a result. Leave the tab open, then run a mutation from another document.
- **Postman** has a WebSocket request type. Point it at `ws://localhost:5100/graphql`, set the `graphql-transport-ws` subprotocol, and send the frames above verbatim. The repo's collection has a WebSocket folder with them ready.
- The one rule that breaks people: the mutation must use the **same** `postId` as the subscription, or it is a different topic and nothing arrives.

## Try it yourself

1. Open the Postman WebSocket request and send `{"type":"connection_init","payload":{}}`; wait for `{"type":"connection_ack"}`.
2. Send the `subscribe` frame for `onPostPublished` (id `"1"`).
3. In Nitro, publish a post and watch a `next` frame arrive:

```graphql
mutation Publish($input: CreatePostInput!) {
  createPost(input: $input) {
    blogPost { id title slug status }
    errors { __typename }
  }
}
```

```json
{ "input": { "title": "Realtime!", "body": "Published live.", "authorId": "QXV0aG9yOjE=", "status": "PUBLISHED" } }
```

4. Send the `subscribe` frame for `onCommentAdded` with `"postId": "QmxvZ1Bvc3Q6Mg=="` (or the id of the post you just created). Then add a comment to that same post:

```graphql
mutation AddComment($input: AddCommentInput!) {
  addComment(input: $input) {
    comment { id text }
    errors { __typename }
  }
}
```

```json
{ "input": { "postId": "QmxvZ1Bvc3Q6Mg==", "authorId": "QXV0aG9yOjE=", "text": "Loud and clear." } }
```

5. Add a comment to a **different** post id. Your subscriber hears nothing — the topic names differ.
6. Finally, start the Blazor client, open a post in two browser tabs, and comment from one; the other updates without a refresh.

## Key takeaways

- A subscription stays open and pushes one result per event; the transport is a WebSocket using `graphql-transport-ws`.
- `[Subscribe]` and `[Topic]` define the stream; `[EventMessage]` is the published value returned to subscribers.
- `OnPostPublished` is a static topic; `OnCommentAdded_{postId}` is dynamic and uses the decoded local id, not the encoded global id.
- Publisher and subscriber must agree on the exact topic string; `createPost` and `addComment` do.
- Strawberry Shake's `Watch(...)` exposes the event stream; de-dupe by id and dispose the subscription.
- `AddInMemorySubscriptions()` is single-process — a distributed transport is the next step for scale.

## Where to go next

- Revisit the command side: [Mutations and typed errors](04-mutations-and-typed-errors.md).
- How the generated client is built: [The Blazor client](08-the-blazor-client.md).
- The classic tour's subscription section: [graphql-tour.md §9](../docs/graphql-tour.md#9-subscriptions).
- Ready-made WebSocket frames: [graphql-practice.postman_collection.json](../docs/graphql-practice.postman_collection.json).
- Repo overview and ports: [README.md](../README.md).
