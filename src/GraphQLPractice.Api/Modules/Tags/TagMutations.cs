using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Errors;
using GraphQLPractice.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQLPractice.Api.Modules.Tags;

[MutationType]
public static partial class TagMutations
{
    [Error(typeof(SlugAlreadyInUseException))]
    public static async Task<Tag> CreateTagAsync(string name, AppDbContext db, CancellationToken ct)
    {
        if (await db.Tags.AnyAsync(t => t.Name == name, ct))
        {
            throw new SlugAlreadyInUseException(name);
        }

        var tag = new Tag { Name = name };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);

        return tag;
    }

    [Error(typeof(TagNotFoundException))]
    public static async Task<Tag> DeleteTagAsync(
        [ID(nameof(Tag))] int id,
        AppDbContext db,
        CancellationToken ct
    )
    {
        var tag = await db.Tags.FindAsync([id], ct) ?? throw new TagNotFoundException(id);

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);

        return tag;
    }
}
