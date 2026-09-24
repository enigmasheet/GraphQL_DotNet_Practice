using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();

    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(author =>
        {
            author.Property(a => a.Name).HasMaxLength(100).IsRequired();
            author.Property(a => a.Bio).HasMaxLength(1000);
        });

        modelBuilder.Entity<Tag>(tag =>
        {
            tag.Property(t => t.Name).HasMaxLength(50).IsRequired();
            tag.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<BlogPost>(post =>
        {
            post.Property(p => p.Title).HasMaxLength(200).IsRequired();
            post.Property(p => p.Slug).HasMaxLength(220).IsRequired();
            post.Property(p => p.Body).IsRequired();
            post.HasIndex(p => p.Slug).IsUnique();
            post.HasIndex(p => p.AuthorId);

            post.HasOne(p => p.Author)
                .WithMany(a => a.Posts)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);

            post.HasMany(p => p.Tags)
                .WithMany(t => t.Posts)
                .UsingEntity(join => join.ToTable("BlogPostTags"));
        });

        modelBuilder.Entity<Comment>(comment =>
        {
            comment.Property(c => c.Text).HasMaxLength(2000).IsRequired();
            comment.HasIndex(c => c.BlogPostId);

            comment
                .HasOne(c => c.BlogPost)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.BlogPostId)
                .OnDelete(DeleteBehavior.Cascade);

            // No inverse navigation on Author for comments, so configure it explicitly.
            comment
                .HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
