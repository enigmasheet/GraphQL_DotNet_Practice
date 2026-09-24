namespace GraphQLPractice.Api.GraphQL.Errors;

public sealed class AuthorNotFoundException(int authorId)
    : Exception($"Author with id {authorId} was not found.")
{
    public int AuthorId { get; } = authorId;
}
