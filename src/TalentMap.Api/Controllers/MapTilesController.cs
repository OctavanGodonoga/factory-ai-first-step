using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TalentMap.Api.Models;
using TalentMap.Api.Services;

namespace TalentMap.Api.Controllers;

[ApiController]
[Route("api/map")]
public class MapTilesController : ControllerBase
{
    private readonly ILogger<MapTilesController> _logger;
    private readonly IMapPointService _mapPointService;

    public MapTilesController(ILogger<MapTilesController> logger, IMapPointService mapPointService)
    {
        _logger = logger;
        _mapPointService = mapPointService;
    }

    [HttpGet("points/{z:int}/{x:int}/{y:int}.pbf")]
    [AllowAnonymous]
    [EnableRateLimiting("mappoints-read")]
    public async Task<IActionResult> GetTile(int z, int x, int y)
    {
        _logger.LogInformation("GetTile action called. Route: GET api/map/points/{Z}/{X}/{Y}.pbf", z, x, y);

        try
        {
            var bytes = await _mapPointService.GetTileMvtAsync(z, x, y);
            // 30s max-age must stay in sync with MapPointService.TileCacheTtl (the server-side cache TTL).
            Response.Headers.CacheControl = "public, max-age=30";
            return File(bytes, "application/vnd.mapbox-vector-tile");
        }
        catch (MapPointValidationException ex)
        {
            _logger.LogWarning("GetTile validation failed. Errors: {Errors}", string.Join("; ", ex.Errors));
            return BadRequest(new { errors = ex.Errors });
        }
    }
}
