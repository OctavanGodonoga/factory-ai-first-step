using TalentMap.Api.Models;

namespace TalentMap.Api.Services;

public interface IMapPointService
{
    Task<MapPoint> CreateAsync(MapPointRequest request);

    Task<MapPoint?> UpdateAsync(string id, MapPointRequest request);

    Task<IReadOnlyList<MapPointDto>> GetByTileAsync(int z, int x, int y);

    Task<byte[]> GetTileMvtAsync(int z, int x, int y);
}
