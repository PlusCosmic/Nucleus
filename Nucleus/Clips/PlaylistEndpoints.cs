using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Exceptions;

namespace Nucleus.Clips;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("api/playlists").RequireAuthorization();

        group.MapPost("", CreatePlaylist).WithName("CreatePlaylist");
        group.MapGet("", GetPlaylists).WithName("GetPlaylists");
        group.MapGet("{id:guid}", GetPlaylistById).WithName("GetPlaylistById");
        group.MapPut("{id:guid}", UpdatePlaylist).WithName("UpdatePlaylist");
        group.MapDelete("{id:guid}", DeletePlaylist).WithName("DeletePlaylist");

        // Playlist clips endpoints
        group.MapPost("{id:guid}/clips", AddClipsToPlaylist).WithName("AddClipsToPlaylist");
        group.MapDelete("{id:guid}/clips/{clipId:guid}", RemoveClipFromPlaylist).WithName("RemoveClipFromPlaylist");
        group.MapPut("{id:guid}/clips/reorder", ReorderPlaylistClips).WithName("ReorderPlaylistClips");

        // Playlist collaborators endpoints
        group.MapPost("{id:guid}/collaborators", AddCollaboratorToPlaylist).WithName("AddCollaborator");
        group.MapDelete("{id:guid}/collaborators/{userId:guid}", RemoveCollaboratorFromPlaylist).WithName("RemoveCollaborator");
        group.MapGet("{id:guid}/collaborators", GetPlaylistCollaborators).WithName("GetCollaborators");
    }

    private static async Task<Results<Created<Playlist>, UnauthorizedHttpResult, BadRequest<string>>> CreatePlaylist(
        PlaylistService playlistService,
        CreatePlaylistRequest request,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            Playlist playlist = await playlistService.CreatePlaylist(request.Name, request.Description, discordId);
            return TypedResults.Created($"/api/playlists/{playlist.Id}", playlist);
        }
        catch (BadRequestException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<List<PlaylistSummary>>, UnauthorizedHttpResult>> GetPlaylists(
        PlaylistService playlistService,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        List<PlaylistSummary> playlists = await playlistService.GetPlaylistsForUser(discordId);
        return TypedResults.Ok(playlists);
    }

    private static async Task<Results<Ok<PlaylistWithDetails>, NotFound, UnauthorizedHttpResult>> GetPlaylistById(
        PlaylistService playlistService,
        Guid id,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        PlaylistWithDetails? playlist = await playlistService.GetPlaylistById(id, discordId);
        if (playlist == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(playlist);
    }

    private static async Task<Results<Ok<Playlist>, NotFound, UnauthorizedHttpResult, BadRequest<string>>> UpdatePlaylist(
        PlaylistService playlistService,
        Guid id,
        UpdatePlaylistRequest request,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            Playlist? playlist = await playlistService.UpdatePlaylist(id, discordId, request.Name, request.Description);
            if (playlist == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(playlist);
        }
        catch (BadRequestException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeletePlaylist(
        PlaylistService playlistService,
        Guid id,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        bool deleted = await playlistService.DeletePlaylist(id, discordId);
        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    // Playlist Clips Endpoints

    private static async Task<Results<Ok<PlaylistWithDetails>, NotFound, UnauthorizedHttpResult, BadRequest<string>>> AddClipsToPlaylist(
        PlaylistService playlistService,
        Guid id,
        AddClipToPlaylistRequest request,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            PlaylistWithDetails? playlist;

            // Handle single clip or batch add
            if (request.ClipId.HasValue)
            {
                playlist = await playlistService.AddClipToPlaylist(id, request.ClipId.Value, discordId);
            }
            else if (request.ClipIds != null && request.ClipIds.Count > 0)
            {
                playlist = await playlistService.AddClipsToPlaylist(id, request.ClipIds, discordId);
            }
            else
            {
                return TypedResults.BadRequest("Either clipId or clipIds must be provided");
            }

            if (playlist == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(playlist);
        }
        catch (BadRequestException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> RemoveClipFromPlaylist(
        PlaylistService playlistService,
        Guid id,
        Guid clipId,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        bool removed = await playlistService.RemoveClipFromPlaylist(id, clipId, discordId);
        if (!removed)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<PlaylistWithDetails>, NotFound, UnauthorizedHttpResult, BadRequest<string>>> ReorderPlaylistClips(
        PlaylistService playlistService,
        Guid id,
        ReorderPlaylistClipsRequest request,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        if (request.ClipOrdering == null || request.ClipOrdering.Count == 0)
        {
            return TypedResults.BadRequest("clipOrdering must be provided and cannot be empty");
        }

        PlaylistWithDetails? playlist = await playlistService.ReorderPlaylistClips(id, request.ClipOrdering, discordId);
        if (playlist == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(playlist);
    }

    // Playlist Collaborators Endpoints

    private static async Task<Results<Ok<List<PlaylistCollaborator>>, NotFound, UnauthorizedHttpResult, BadRequest<string>>> AddCollaboratorToPlaylist(
        PlaylistService playlistService,
        Guid id,
        AddCollaboratorRequest request,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        if (!request.UserId.HasValue && string.IsNullOrWhiteSpace(request.Username))
        {
            return TypedResults.BadRequest("Either userId or username must be provided");
        }

        try
        {
            List<PlaylistCollaborator>? collaborators = await playlistService.AddCollaborator(
                id,
                discordId,
                request.UserId,
                request.Username
            );

            if (collaborators == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(collaborators);
        }
        catch (BadRequestException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest<string>>> RemoveCollaboratorFromPlaylist(
        PlaylistService playlistService,
        Guid id,
        Guid userId,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            bool removed = await playlistService.RemoveCollaborator(id, userId, discordId);
            if (!removed)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }
        catch (BadRequestException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<List<PlaylistCollaborator>>, NotFound, UnauthorizedHttpResult>> GetPlaylistCollaborators(
        PlaylistService playlistService,
        Guid id,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        List<PlaylistCollaborator>? collaborators = await playlistService.GetCollaborators(id, discordId);
        if (collaborators == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(collaborators);
    }
}