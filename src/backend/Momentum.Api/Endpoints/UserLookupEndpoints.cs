using Asp.Versioning;
using Asp.Versioning.Builder;
using Mediator;
using Momentum.Application.Abstractions;
using Momentum.Application.Features.Auth;

namespace Momentum.Api.Endpoints;

/// <summary>
/// IS-EMRI-o86-A §F: <c>POST /v1/users/lookup</c> (e-posta arama ucu -- davet bootstrap'i icin).
/// 🔴 GET + query string YASAK (e-posta URL'de/loglarda/Referer'da kalir) -- POST govde ile.
/// 🔴 Kimlik dogrulamasi SART (401) -- aksi halde acik kullanici sayim yuzeyi doğar.
/// 🔴 Yanit YALNIZ userId -- ad/e-posta/olusturma tarihi DONMEZ (anayasa §8, amacla sinirlilik;
/// OrSet'e de e-posta YAZILMAZ, members elemani userId'dir).
/// </summary>
public static class UserLookupEndpoints
{
    public static void Map(IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var v1 = app.MapGroup("/v{version:apiVersion}/users").WithApiVersionSet(versionSet);

        v1.MapPost("/lookup", HandleLookupAsync).MapToApiVersion(new ApiVersion(1, 0)).WithName("UserLookup");
    }

    private static async Task<IResult> HandleLookupAsync(
        UserLookupRequest request, ICurrentUser currentUser, IMediator mediator, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return Results.Unauthorized();
        }

        var userId = await mediator.Send(new LookupUserQuery(request.Email), cancellationToken);
        return userId is { } id ? Results.Ok(new { userId = id }) : Results.NotFound();
    }
}

public sealed record UserLookupRequest(string Email);
