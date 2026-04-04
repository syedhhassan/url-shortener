using SolidShortener.Application.Users.Commands;
using SolidShortener.Application.Users.Queries;
using SolidShortener.Application.Users.Services.Interfaces;

namespace SolidShortener.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await _userService.RegisterUserAsync(
            new RegisterUserCommand { Name = request.Name, Email = request.Email, Password = request.Password });

        return StatusCode(201, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _userService.AuthenticateAsync(
            new AuthenticateUserQuery { Email = request.Email, Password = request.Password });

        if (response is null) return Unauthorized(new { message = "Invalid email or password" });

        return Ok(response);
    }
}
