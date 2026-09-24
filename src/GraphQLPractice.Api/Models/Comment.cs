namespace GraphQLPractice.Api.Models;

public sealed class Comment
{
    public int Id { get; set; }

    public string Text { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }

    public int BlogPostId { get; set; }

    public int AuthorId { get; set; }

    // Resolved by CommentNode via DataLoaders (blogPost -> "post").
    [GraphQLIgnore]
    public BlogPost BlogPost { get; set; } = default!;

    [GraphQLIgnore]
    public Author Author { get; set; } = default!;
}
