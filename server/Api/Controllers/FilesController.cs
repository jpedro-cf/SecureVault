using System.Security.Claims;
using EncryptionApp.Api.Dtos.Files;
using EncryptionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptionApp.Api.Controllers;

[ApiController]
[Route("files")]
[EnableRateLimiting("user_limiter")]
public class FilesController(FilesService filesService, UploadsService uploadsService): ControllerBase
{
    [HttpPost("upload")]
    [Authorize]
    public async Task<IResult> Upload([FromBody] UploadFileRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await uploadsService.Upload(Guid.Parse(userId), data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }
    [HttpPost("{id}/complete-upload")]
    [Authorize]
    public async Task<IResult> CompleteUpload([FromRoute] Guid id, [FromBody] CompleteUploadRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await uploadsService.CompleteUpload(Guid.Parse(userId), id, data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }
    
    [HttpPost("{id}/cancel-upload")]
    [Authorize]
    public async Task<IResult> CancelUpload([FromRoute] Guid fileId, [FromBody] CancelUploadRequest data)
    {
        if (!ModelState.IsValid)
        {
            return Results.BadRequest(data);
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await uploadsService.CancelUploadWithTransaction(Guid.Parse(userId), fileId, data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.NoContent();
    }
    
    [HttpGet("{id}")]
    [EnableRateLimiting("public_limiter")]
    public async Task<IResult> Get([FromRoute] string id, [FromQuery] GetFileRequest data)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await filesService.GetFile(Guid.Parse(id), userId != null ? Guid.Parse(userId) : null, data);
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IResult> Delete([FromRoute] string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await filesService.DeleteFile(Guid.Parse(userId), Guid.Parse(id));
        
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.NoContent();
    }
}