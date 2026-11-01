namespace Relifes.Api.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string ApiKeyHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
