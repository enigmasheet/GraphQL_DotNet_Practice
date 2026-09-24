using GraphQLPractice.Api.Models;
using HotChocolate.Subscriptions;

namespace GraphQLPractice.Api.Modules.Posts;

[SubscriptionType]
public static partial class PostSubscription
{
    [Subscribe]
    [Topic("OnPostPublished")]
    public static BlogPost OnPostPublished([EventMessage] BlogPost post) => post;
}
