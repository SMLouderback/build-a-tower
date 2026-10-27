using System.Security.Claims;
using CloudSave.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Saves;

public static class SaveGateEndpoints
{
    public static IEndpointRouteBuilder MapSaveGateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/v1/saves/{slotId}", PutSavePlaceholder)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> PutSavePlaceholder(
        [FromRoute] string slotId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return Results.Unauthorized();

        if (!user.EmailConfirmed)
            return Results.Json(new { code = "email_unverified" }, statusCode: StatusCodes.Status403Forbidden);

        return Results.NoContent();
    }
}
