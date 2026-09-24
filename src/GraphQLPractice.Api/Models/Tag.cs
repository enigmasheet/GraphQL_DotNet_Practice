namespace GraphQLPractice.Api.Models;

[Node]
public sealed class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    // The blog posts for a tag are exposed through the Posts module, not the Tag type.
    [GraphQLIgnore]
    public ICollection<BlogPost> Posts { get; set; } = [];
}
