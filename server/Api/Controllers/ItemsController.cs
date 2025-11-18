using System.Security.Claims;
using EncryptionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptionApp.Api.Controllers;

[ApiController]
[Route("items")]
[EnableRateLimiting("user_limiter")]
public class ItemsController(ItemsService itemsService): ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> GetItems()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var result = await itemsService.GetAll(Guid.Parse(userId));
        return !result.IsSuccess ? result.Error!.ToHttpResult() : Results.Ok(result.Data);
    }
}