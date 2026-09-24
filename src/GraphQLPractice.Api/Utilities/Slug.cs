using System.Text;

namespace GraphQLPractice.Api.Utilities;

public static class Slug
{
    public static string From(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? Guid.NewGuid().ToString("n")[..8] : slug;
    }
}
