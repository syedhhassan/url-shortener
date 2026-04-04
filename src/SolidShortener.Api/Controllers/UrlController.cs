using SolidShortener.Api.Services;
using SolidShortener.Application.Urls.Commands;
using SolidShortener.Application.Urls.Queries;
using SolidShortener.Application.Urls.Services.Interfaces;
using SolidShortener.Application.Visits.Commands;
using SolidShortener.Application.Visits.Services.Interfaces;

namespace SolidShortener.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UrlController : ControllerBase
{
    private readonly IUrlService _urlService;
    private readonly IVisitService _visitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UrlController> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public UrlController(IUrlService urlService, IVisitService visitService, ICurrentUserService currentUserService, ILogger<UrlController> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _urlService = urlService;
        _visitService = visitService;
        _currentUserService = currentUserService;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    [Authorize]
    [HttpPost("shorten")]
    public async Task<IActionResult> Shorten([FromBody] ShortenUrlRequest request)
    {
        var userId = _currentUserService.UserId;
        if (userId == Guid.Empty) return BadRequest("Invalid user id");

        var response = await _urlService.ShortenUrlAsync(new ShortenUrlCommand { UserId = userId, LongUrl = request.LongUrl, ExpiresAt = request.ExpiresAt });
        return Ok(response);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetUrl(string code)
    {
        var response = await _urlService.GetUrlByShortCodeAsync(
            new GetUrlByShortCodeQuery { ShortCode = code });

        if (response is null) return NotFound();

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var visitService = scope.ServiceProvider.GetRequiredService<IVisitService>();
            try
            {
                await visitService.LogVisitAsync(new LogVisitCommand
                {
                    ShortCode = code,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log visit for short code {ShortCode}", code);
            }
        });

        return Redirect(response.LongUrl);
    }

    [Authorize]
    [HttpGet()]
    public async Task<IActionResult> GetUrlsByUser()
    {
        var userId = _currentUserService.UserId;
        if (userId == Guid.Empty) return Unauthorized();

        var urls = await _urlService.GetUrlsByUserAsync(new GetUrlsByUserQuery { UserId = userId });
        return Ok(urls);
    }

    [Authorize]
    [HttpDelete("{code}")]
    public async Task<IActionResult> DeleteUrl(string code)
    {
        var userId = _currentUserService.UserId;
        if (userId == Guid.Empty) return Unauthorized();

        await _urlService.DeleteUrlAsync(new DeleteUrlCommand { UserId = userId, ShortCode = code });
        return NoContent();
    }
}
