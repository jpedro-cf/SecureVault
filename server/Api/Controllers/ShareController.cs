using System.Security.Claims;
using EncryptionApp.Api.Dtos.Share;
using EncryptionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptionApp.Api.Controllers;

[ApiController]
[Route("shared-links")]
[EnableRateLimiting("user_limiter")]
public class ShareController(ShareService shareService): ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IResult> Create([FromBody] CreateSharedLinkRequest request)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(request);
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await shareService.CreateShare(Guid.Parse(userId), request);

        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }
    
    [HttpGet]
    [Authorize]
    public async Task<IResult> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await shareService.GetSharedLinks(Guid.Parse(userId));
        
        return Results.Ok(result);
    }
    
    [HttpGet("{id}")]
    [EnableRateLimiting("public_limiter")]
    public async Task<IResult> Create([FromRoute] string id)
    {
        var result = await shareService.GetSharedContent(Guid.Parse(id));
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }
    
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IResult> Delete([FromRoute] Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await shareService.DeleteShare(Guid.Parse(userId), id);

        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.NoContent();
    }
}