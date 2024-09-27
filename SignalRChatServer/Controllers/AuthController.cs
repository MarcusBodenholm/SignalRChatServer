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

    public AuthController(ChatContext context, IConfiguration config, IAuthServices authServices)
    {
        _context = context;
        _config = config;
        _authService = authServices;
    }
    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] UserDto userDto)
    {
        if (userDto.Password != userDto.ConfirmPassword)
        {
            return BadRequest(new { message = "Passwords do not match" });
        }
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);
        var user = new User { Username = userDto.Username, PasswordHash = hashedPassword };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok("User registered successfully.");
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == loginDto.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return Unauthorized("The username or password was incorrect.");
        }
        var secretKey = _config["SecretKey"];
        var token = _authService.GenerateJwtToken(user, secretKey);
        return Ok(new { Token = token });
    }
}
