using System.Security.Claims;
using EncryptionApp.Api.Dtos.Users;
using EncryptionApp.Api.Entities;
using EncryptionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptionApp.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableRateLimiting("user_limiter")]
public class AuthenticationController(
    UsersService usersService, 
    AuthService authService, 
    SignInManager<User> signInManager) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("public_limiter")]
    public async Task<IResult> Register([FromBody] RegisterUserRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }
        
        var result = await usersService.Create(data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Created();
    }
    
    [HttpPost("login")]
    [EnableRateLimiting("public_limiter")]
    public async Task<IResult> Login([FromBody] LoginRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }

        var result = await authService.Login(data);
        if (!result.IsSuccess)
        {
            return result.Error!.ToHttpResult();
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, 
            Expires = DateTime.Now.AddDays(5), 
            Path = "/", 
            Secure = true, 
            SameSite = SameSiteMode.Lax 
        };
        Response.Cookies.Append("accessToken", result.Data!.Token, cookieOptions);

        return Results.Ok(result.Data);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IResult> Logout()
    {
        Response.Cookies.Delete("accessToken");
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    [HttpGet("mfa")]
    [Authorize]
    public async Task<IResult> GetMfaKey()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await authService.GetMfaKey(Guid.Parse(userId!));
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data!);
    }
    
    [HttpPost("mfa")]
    [Authorize]
    public async Task<IResult> SetupMfa([FromBody] SetupMfaRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await authService.SetupMfa(Guid.Parse(userId!), data.Code);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.NoContent();
    }
    
    [HttpPost("mfa/login")]
    public async Task<IResult> LoginMfa([FromBody] LoginMfaRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }
        
        var result = await authService.LoginMfa(data.Code);
        if (!result.IsSuccess)
        {
            return result.Error!.ToHttpResult();
        }
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, 
            Expires = DateTime.Now.AddDays(5), 
            Path = "/", 
            Secure = true, 
            SameSite = SameSiteMode.Lax 
        };
        Response.Cookies.Append("accessToken", result.Data!.Token, cookieOptions);
        
        return Results.Ok(result.Data);
    }
}