namespace GraphQLPractice.Api.Models;

[Node(
    NodeResolverType = typeof(Modules.Posts.PostNodeResolver),
    NodeResolver = nameof(Modules.Posts.PostNodeResolver.GetPostAsync)
)]
public sealed class BlogPost
{
    public int Id { get; set; }

    public string Title { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public string Body { get; set; } = default!;

    public PostStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int AuthorId { get; set; }

    // Navigations are resolved by BlogPostNode via DataLoaders.
    [GraphQLIgnore]
    public Author Author { get; set; } = default!;

    [GraphQLIgnore]
    public ICollection<Comment> Comments { get; set; } = [];

    [GraphQLIgnore]
    public ICollection<Tag> Tags { get; set; } = [];
}
