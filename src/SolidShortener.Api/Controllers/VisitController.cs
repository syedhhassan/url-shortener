using SolidShortener.Api.Services;
using SolidShortener.Application.Visits.Queries;
using SolidShortener.Application.Visits.Services.Interfaces;

namespace SolidShortener.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VisitController : ControllerBase
{
    private readonly IVisitService _visitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<VisitController> _logger;

    public VisitController(IVisitService visitService, ICurrentUserService currentUserService, ILogger<VisitController> logger)
    {
        _visitService = visitService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("count/{shortCode}")]
    public async Task<IActionResult> GetVisitCountByShortCode(string shortCode)
    {
        var userId = _currentUserService.UserId;
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _visitService.GetVisitCountByShortCodeAsync(new GetVisitCountByShortCodeQuery { UserId = userId, ShortCode = shortCode });
        return Ok(count);
    }

    [Authorize]
    [HttpGet("visits/{shortCode}")]
    public async Task<IActionResult> GetVisitsByShortCode(string shortCode)
    {
        var userId = _currentUserService.UserId;
        if (userId == Guid.Empty) return Unauthorized();

        var visits = await _visitService.GetVisitsByShortCodeAsync(new GetVisitsByShortCodeQuery { UserId = userId, ShortCode = shortCode });
        return Ok(visits);
    }
}
