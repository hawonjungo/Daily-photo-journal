namespace Relifes.Api.Entities;

public class Place
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? GooglePlaceId { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }

    // Nullable because a manually typed place name may have no location.
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
