namespace TalentMap.Api.Models;

public class MapPointValidationException(IReadOnlyList<string> errors) : Exception
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
