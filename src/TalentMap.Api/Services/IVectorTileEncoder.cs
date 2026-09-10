using TalentMap.Api.Models;

namespace TalentMap.Api.Services;

public interface IVectorTileEncoder
{
    byte[] Encode(IReadOnlyList<MapPoint> points, int z, int x, int y);
}
