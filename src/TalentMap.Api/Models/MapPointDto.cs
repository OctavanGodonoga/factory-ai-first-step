namespace TalentMap.Api.Models;

public class MapPointDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public double Longitude { get; set; }

    public double Latitude { get; set; }
}
