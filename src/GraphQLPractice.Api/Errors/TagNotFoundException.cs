namespace GraphQLPractice.Api.Errors;

public sealed class TagNotFoundException(int tagId)
    : Exception($"Tag with id {tagId} was not found.")
{
    public int TagId { get; } = tagId;
}
