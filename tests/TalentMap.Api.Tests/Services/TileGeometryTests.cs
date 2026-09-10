using TalentMap.Api.Services;
using Xunit;

namespace TalentMap.Api.Tests.Services;

public class TileGeometryTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, 1)]
    public void ToPolygon_RejectsLowZoomTilesThatWouldSpanTooManyDegreesOfLongitude(int z, int x, int y)
    {
        // At z=0/z=1 the tile spans >= 180 degrees of longitude, an edge MongoDB's
        // $geoWithin rejects, so these zoom levels must be validated out before querying.
        Assert.Throws<ArgumentOutOfRangeException>(() => TileGeometry.ToPolygon(z, x, y));
    }

    [Fact]
    public void ToPolygon_AtMinimumSupportedZoom_ProducesAPolygonUnder180DegreesOfLongitude()
    {
        var polygon = TileGeometry.ToPolygon(2, 0, 0);

        var positions = polygon.Coordinates.Exterior.Positions;
        var minLon = positions.Min(p => p.Longitude);
        var maxLon = positions.Max(p => p.Longitude);

        Assert.True(maxLon - minLon < 180, $"Longitude span {maxLon - minLon} must stay under 180 degrees.");
    }

    [Fact]
    public void ToPolygon_ClosesTheRing()
    {
        var polygon = TileGeometry.ToPolygon(2, 1, 1);

        var positions = polygon.Coordinates.Exterior.Positions;

        Assert.Equal(5, positions.Count);
        Assert.Equal(positions[0].Longitude, positions[^1].Longitude);
        Assert.Equal(positions[0].Latitude, positions[^1].Latitude);
    }
}
