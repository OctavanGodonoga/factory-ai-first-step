namespace TalentMap.Api.Services;

public interface IMoldovaBorderValidator
{
    bool IsInside(double longitude, double latitude);
}
