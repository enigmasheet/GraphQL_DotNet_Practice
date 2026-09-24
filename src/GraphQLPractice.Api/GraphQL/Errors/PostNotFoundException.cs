namespace GraphQLPractice.Api.GraphQL.Errors;

public sealed class PostNotFoundException(int postId)
    : Exception($"Post with id {postId} was not found.")
{
    public int PostId { get; } = postId;
}
