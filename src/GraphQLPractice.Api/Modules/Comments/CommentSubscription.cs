using GraphQLPractice.Api.Models;
using HotChocolate.Subscriptions;

namespace GraphQLPractice.Api.Modules.Comments;

[SubscriptionType]
public static partial class CommentSubscription
{
    // Dynamic topic: {postId} is replaced with the decoded argument value (a local
    // int, because [ID] is deserialized here), so it matches the topic the mutation
    // publishes to. The argument itself is a global ID so clients pass the same id
    // they use for node(id:) and everywhere else.
    [Subscribe]
    [Topic("OnCommentAdded_{postId}")]
    public static Comment OnCommentAdded(
        [ID(nameof(BlogPost))] int postId,
        [EventMessage] Comment comment
    ) => comment;
}
