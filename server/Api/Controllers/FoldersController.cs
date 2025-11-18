using System.Security.Claims;
using EncryptionApp.Api.Dtos.Folders;
using EncryptionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptionApp.Api.Controllers;

[ApiController]
[Route("folders")]
[EnableRateLimiting("user_limiter")]
public class FoldersController(FoldersService foldersService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IResult> Create([FromBody] CreateFolderRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await foldersService.Create(Guid.Parse(userId), data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }

    [HttpGet("{id}")]
    [EnableRateLimiting("public_limiter")]
    public async Task<IResult> GetFolder([FromRoute] Guid id, [FromQuery] GetFolderRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await foldersService.GetFolder(id, userId != null ? Guid.Parse(userId): null, data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }
    
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IResult> Delete([FromRoute] Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await foldersService.DeleteFolder(Guid.Parse(userId), id);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.NoContent();
    }
}