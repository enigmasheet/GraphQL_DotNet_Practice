namespace GraphQLPractice.Api.Models;

public sealed class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public ICollection<BlogPost> Posts { get; set; } = [];
}
