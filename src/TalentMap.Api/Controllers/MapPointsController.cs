using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TalentMap.Api.Models;
using TalentMap.Api.Services;

namespace TalentMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("mappoints-write")]
public class MapPointsController : ControllerBase
{
    private readonly ILogger<MapPointsController> _logger;
    private readonly IMapPointService _mapPointService;

    public MapPointsController(ILogger<MapPointsController> logger, IMapPointService mapPointService)
    {
        _logger = logger;
        _mapPointService = mapPointService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MapPointRequest request)
    {
        _logger.LogInformation("Create action called. Route: POST api/mappoints");

        try
        {
            var result = await _mapPointService.CreateAsync(request);
            return Created($"/api/mappoints/{result.Id}", result);
        }
        catch (MapPointValidationException ex)
        {
            _logger.LogWarning("Create validation failed. Errors: {Errors}", string.Join("; ", ex.Errors));
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] MapPointRequest request)
    {
        _logger.LogInformation("Update action called. Route: PUT api/mappoints/{Id}", id);

        try
        {
            var result = await _mapPointService.UpdateAsync(id, request);
            if (result is null)
            {
                _logger.LogWarning("Update target not found. Id: {Id}", id);
                return NotFound();
            }

            return Ok(result);
        }
        catch (MapPointValidationException ex)
        {
            _logger.LogWarning("Update validation failed. Errors: {Errors}", string.Join("; ", ex.Errors));
            return BadRequest(new { errors = ex.Errors });
        }
    }
}
