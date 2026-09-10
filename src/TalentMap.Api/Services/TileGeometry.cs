using MongoDB.Driver.GeoJsonObjectModel;

namespace TalentMap.Api.Services;

public static class TileGeometry
{
    private const int MinZoom = 2;
    private const int MaxZoom = 22;

    public static (double MinLon, double MinLat, double MaxLon, double MaxLat) ToBoundingBox(int z, int x, int y)
    {
        ValidateTileCoordinates(z, x, y);

        var minLon = TileToLongitude(x, z);
        var maxLon = TileToLongitude(x + 1, z);
        var maxLat = TileToLatitude(y, z);
        var minLat = TileToLatitude(y + 1, z);

        return (minLon, minLat, maxLon, maxLat);
    }

    public static GeoJsonPolygon<GeoJson2DGeographicCoordinates> ToPolygon(int z, int x, int y)
    {
        var (minLon, minLat, maxLon, maxLat) = ToBoundingBox(z, x, y);

        var coordinates = new[]
        {
            new GeoJson2DGeographicCoordinates(minLon, minLat),
            new GeoJson2DGeographicCoordinates(maxLon, minLat),
            new GeoJson2DGeographicCoordinates(maxLon, maxLat),
            new GeoJson2DGeographicCoordinates(minLon, maxLat),
            new GeoJson2DGeographicCoordinates(minLon, minLat),
        };

        var ring = new GeoJsonLinearRingCoordinates<GeoJson2DGeographicCoordinates>(coordinates);
        return new GeoJsonPolygon<GeoJson2DGeographicCoordinates>(new GeoJsonPolygonCoordinates<GeoJson2DGeographicCoordinates>(ring));
    }

    private static void ValidateTileCoordinates(int z, int x, int y)
    {
        if (z < MinZoom || z > MaxZoom)
        {
            throw new ArgumentOutOfRangeException(
                nameof(z),
                z,
                $"Zoom trebuie să fie între {MinZoom} și {MaxZoom}. Zoom-urile sub {MinZoom} produc un poligon " +
                "cu o latură de cel puțin 180 de grade longitudine, pe care MongoDB $geoWithin îl respinge.");
        }

        var maxIndex = (1 << z) - 1;

        if (x < 0 || x > maxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X trebuie să fie între 0 și {maxIndex} pentru zoom {z}.");
        }

        if (y < 0 || y > maxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y trebuie să fie între 0 și {maxIndex} pentru zoom {z}.");
        }
    }

    private static double TileToLongitude(int x, int z)
    {
        return (x / (double)(1 << z) * 360.0) - 180.0;
    }

    private static double TileToLatitude(int y, int z)
    {
        var n = Math.PI - (2.0 * Math.PI * y / (1 << z));
        return 180.0 / Math.PI * Math.Atan(Math.Sinh(n));
    }
}
