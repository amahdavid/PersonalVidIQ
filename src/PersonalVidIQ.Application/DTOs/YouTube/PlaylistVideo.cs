namespace PersonalVidIQ.Application.DTOs.YouTube;

/// <summary>One entry of an uploads playlist (playlistItems.list), newest first.</summary>
public sealed record PlaylistVideo(string VideoId, string Title, DateTimeOffset? PublishedAtUtc);
