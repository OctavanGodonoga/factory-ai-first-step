using TalentMap.Api.Models;

namespace TalentMap.Api.Repositories;

public interface IMapPointRepository
{
    Task<MapPoint?> FindByIdAsync(string id);

    Task<MapPoint> InsertAsync(MapPoint point);

    Task<bool> ReplaceAsync(string id, MapPoint point);
}
