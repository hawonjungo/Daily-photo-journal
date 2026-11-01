namespace Relifes.Api.Entities;

/// <summary>Google calls made per UTC day. The cap protects one shared billing account, so this has no UserId.</summary>
public class GoogleApiUsage
{
    public DateOnly Date { get; set; }
    public int CallCount { get; set; }
}
