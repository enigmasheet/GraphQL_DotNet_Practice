using GraphQLPractice.Api.Models;
using HotChocolate.Subscriptions;

namespace GraphQLPractice.Api.Modules.Comments;

[SubscriptionType]
public static partial class CommentSubscription
{
    // Dynamic topic: {postId} is replaced with the argument value, so a client
    // subscribing with a specific postId only receives that post's comments.
    [Subscribe]
    [Topic("OnCommentAdded_{postId}")]
    public static Comment OnCommentAdded(int postId, [EventMessage] Comment comment) => comment;
}
