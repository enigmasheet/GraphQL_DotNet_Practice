namespace GraphQLPractice.Api.Models;

[Node(
    NodeResolverType = typeof(Modules.Authors.AuthorNodeResolver),
    NodeResolver = nameof(Modules.Authors.AuthorNodeResolver.GetAuthorAsync)
)]
public sealed class Author
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Bio { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Resolved by AuthorNode via a DataLoader, so the raw navigation stays out of the schema.
    [GraphQLIgnore]
    public ICollection<BlogPost> Posts { get; set; } = [];
}
