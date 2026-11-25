using System;

namespace Jellyfin.Plugin.FilmDiary.Data;

/// <summary>
/// Puntuación, notas y visionados de un usuario para una película.
/// </summary>
public class DiaryEntry
{
    /// <summary>Gets or sets the item id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the rating, from 0.5 to 5 in steps of 0.5. Null means not rated.</summary>
    public double? Rating { get; set; }

    /// <summary>Gets or sets the free-text notes (review).</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the date the film was watched (yyyy-MM-dd), if logged.</summary>
    public string? WatchedDate { get; set; }

    /// <summary>Gets or sets a value indicating whether this was a rewatch.</summary>
    public bool Rewatch { get; set; }

    /// <summary>Gets or sets a value indicating whether the user likes the film (heart).</summary>
    public bool Liked { get; set; }

    /// <summary>Gets or sets the last update time (UTC).</summary>
    public DateTime UpdatedUtc { get; set; }
}
