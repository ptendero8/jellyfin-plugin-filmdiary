using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.FilmDiary.Data;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.FilmDiary.Api;

/// <summary>
/// API del diario: puntuaciones, notas, visionados y exportación.
/// </summary>
[ApiController]
[Route("FilmDiary")]
public class FilmDiaryController : ControllerBase
{
    private readonly DiaryStore _store;
    private readonly IAuthorizationContext _auth;
    private readonly ILibraryManager _library;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilmDiaryController"/> class.
    /// </summary>
    /// <param name="store">Almacén del diario.</param>
    /// <param name="auth">Contexto de autorización.</param>
    /// <param name="library">Biblioteca.</param>
    public FilmDiaryController(DiaryStore store, IAuthorizationContext auth, ILibraryManager library)
    {
        _store = store;
        _auth = auth;
        _library = library;
    }

    /// <summary>Script de la interfaz web (público: no contiene datos).</summary>
    /// <returns>JavaScript.</returns>
    [HttpGet("client.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("Jellyfin.Plugin.FilmDiary.Web.filmdiary.js");
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-cache";
        return File(stream, "application/javascript; charset=utf-8");
    }

    /// <summary>Entrada del usuario actual para una película.</summary>
    /// <param name="itemId">Película.</param>
    /// <returns>La entrada (vacía si no existe).</returns>
    [HttpGet("Items/{itemId}")]
    [Authorize]
    public async Task<ActionResult<DiaryEntry>> GetEntry([FromRoute] Guid itemId)
    {
        var userId = await CurrentUser().ConfigureAwait(false);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        return _store.Get(userId, itemId) ?? new DiaryEntry { ItemId = itemId };
    }

    /// <summary>Guarda la entrada del usuario actual para una película.</summary>
    /// <param name="itemId">Película.</param>
    /// <param name="body">Datos.</param>
    /// <returns>La entrada guardada.</returns>
    [HttpPost("Items/{itemId}")]
    [Authorize]
    public async Task<ActionResult<DiaryEntry>> SaveEntry([FromRoute] Guid itemId, [FromBody] DiaryEntry body)
    {
        var userId = await CurrentUser().ConfigureAwait(false);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (_library.GetItemById(itemId) is null)
        {
            return NotFound();
        }

        if (body.Rating is double r)
        {
            if (r <= 0)
            {
                body.Rating = null;
            }
            else
            {
                body.Rating = Math.Clamp(Math.Round(r * 2, MidpointRounding.AwayFromZero) / 2, 0.5, 5);
            }
        }

        if (!string.IsNullOrEmpty(body.WatchedDate)
            && !DateOnly.TryParseExact(body.WatchedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return BadRequest("WatchedDate debe tener formato yyyy-MM-dd");
        }

        if (body.Notes is { Length: > 20000 })
        {
            body.Notes = body.Notes[..20000];
        }

        body.ItemId = itemId;
        var saved = _store.Save(userId, body);
        return saved ?? new DiaryEntry { ItemId = itemId };
    }

    /// <summary>Borra la entrada del usuario actual para una película.</summary>
    /// <param name="itemId">Película.</param>
    /// <returns>Sin contenido.</returns>
    [HttpDelete("Items/{itemId}")]
    [Authorize]
    public async Task<ActionResult> DeleteEntry([FromRoute] Guid itemId)
    {
        var userId = await CurrentUser().ConfigureAwait(false);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        _store.Delete(userId, itemId);
        return NoContent();
    }

    /// <summary>Todo el diario del usuario actual, con título y año.</summary>
    /// <returns>Lista.</returns>
    [HttpGet("Entries")]
    [Authorize]
    public async Task<ActionResult> GetEntries()
    {
        var userId = await CurrentUser().ConfigureAwait(false);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var list = _store.GetAll(userId).Select(e =>
        {
            var item = _library.GetItemById(e.ItemId);
            return new
            {
                e.ItemId,
                e.Rating,
                e.Notes,
                e.WatchedDate,
                e.Rewatch,
                e.Liked,
                e.UpdatedUtc,
                Name = item?.Name,
                Year = item?.ProductionYear,
            };
        });
        return Ok(list);
    }

    /// <summary>Exporta el diario a CSV con las columnas que acepta el importador de Letterboxd.</summary>
    /// <returns>Fichero CSV.</returns>
    [HttpGet("Export/Letterboxd")]
    [Authorize]
    [Produces("text/csv")]
    public async Task<ActionResult> ExportLetterboxd()
    {
        var userId = await CurrentUser().ConfigureAwait(false);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var sb = new StringBuilder();
        sb.AppendLine("Title,Year,imdbID,tmdbID,Rating,WatchedDate,Rewatch,Review");
        foreach (var e in _store.GetAll(userId))
        {
            var item = _library.GetItemById(e.ItemId);
            if (item is null)
            {
                continue;
            }

            var title = item.OriginalTitle is { Length: > 0 } ot && ot != item.Name ? ot : item.Name;
            item.ProviderIds.TryGetValue(MediaBrowser.Model.Entities.MetadataProvider.Imdb.ToString(), out var imdb);
            item.ProviderIds.TryGetValue(MediaBrowser.Model.Entities.MetadataProvider.Tmdb.ToString(), out var tmdb);
            sb.Append(Csv(title)).Append(',')
              .Append(item.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
              .Append(Csv(imdb)).Append(',')
              .Append(Csv(tmdb)).Append(',')
              .Append(e.Rating?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
              .Append(e.WatchedDate ?? string.Empty).Append(',')
              .Append(e.Rewatch ? "true" : "false").Append(',')
              .Append(Csv(e.Notes))
              .AppendLine();
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "film-diary-letterboxd.csv");
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private async Task<Guid> CurrentUser()
    {
        var info = await _auth.GetAuthorizationInfo(Request).ConfigureAwait(false);
        return info?.UserId ?? Guid.Empty;
    }
}
