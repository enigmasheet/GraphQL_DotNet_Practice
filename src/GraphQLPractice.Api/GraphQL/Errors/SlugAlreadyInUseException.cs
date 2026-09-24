namespace GraphQLPractice.Api.GraphQL.Errors;

public sealed class SlugAlreadyInUseException(string slug)
    : Exception($"A post with slug '{slug}' already exists.")
{
    public string Slug { get; } = slug;
}
