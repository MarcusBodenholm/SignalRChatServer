using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalRChatServer.Infrastructure.Context;
using SignalRChatServer.Infrastructure.DTOs;
using SignalRChatServer.Infrastructure.Models;
using SignalRChatServer.Infrastructure.Services;

namespace SignalRChatServer.API.Controllers;
public class AuthController : Controller
{
    //TODO - Add logging
    private readonly ChatContext _context;
    private readonly IConfiguration _config;
    private readonly IAuthServices _authService;
    private readonly ILogger _logger;

    public AuthController(ChatContext context, IConfiguration config, IAuthServices authServices, ILogger logger)
    {
        _context = context;
        _config = config;
        _authService = authServices;
        _logger = logger;
    }
    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] UserDto userDto)
    {
        _logger.LogInformation("User attempts signup");
        try
        {
            if (userDto.Password != userDto.ConfirmPassword)
            {
                _logger.LogInformation("User signup failed due to passwords not matching.");

                return BadRequest(new { message = "Passwords do not match" });
            }
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);
            var user = new User { Username = userDto.Username, PasswordHash = hashedPassword };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User signup successful.");
            return Ok("User registered successfully.");

        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR AuthController > Signup :: {ex.Message}");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);

        }
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        _logger.LogInformation("User attempts login");

        try
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == loginDto.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                _logger.LogInformation("Login failed due to incorrect username or password.");
                return Unauthorized("The username or password was incorrect.");
            }
            var secretKey = _config["SecretKey"];
            var token = _authService.GenerateJwtToken(user, secretKey);
            _logger.LogInformation("Login successful");
            return Ok(new { Token = token });

        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR AuthController > Login :: {ex.Message}");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);

        }
    }
}
