using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraphQLPractice.Api.Modules.Posts;

internal sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(220).IsRequired();
        builder.Property(p => p.Body).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.AuthorId);

        builder
            .HasOne(p => p.Author)
            .WithMany(a => a.Posts)
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(p => p.Tags)
            .WithMany(t => t.Posts)
            .UsingEntity(join => join.ToTable("BlogPostTags"));
    }
}
