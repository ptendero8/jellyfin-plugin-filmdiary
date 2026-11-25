using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FilmDiary.Data;

/// <summary>
/// Guarda el diario de cada usuario en un fichero JSON dentro de la carpeta de datos de Jellyfin.
/// Un fichero por usuario: &lt;data&gt;/filmdiary/&lt;userId&gt;.json.
/// </summary>
public class DiaryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly ILogger<DiaryStore> _logger;
    private readonly Dictionary<Guid, Dictionary<Guid, DiaryEntry>> _cache = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DiaryStore"/> class.
    /// </summary>
    /// <param name="paths">Rutas de la aplicación.</param>
    /// <param name="logger">Logger.</param>
    public DiaryStore(IApplicationPaths paths, ILogger<DiaryStore> logger)
    {
        _folder = Path.Combine(paths.DataPath, "filmdiary");
        _logger = logger;
        Directory.CreateDirectory(_folder);
    }

    /// <summary>Devuelve todas las entradas de un usuario.</summary>
    /// <param name="userId">Usuario.</param>
    /// <returns>Entradas.</returns>
    public IReadOnlyList<DiaryEntry> GetAll(Guid userId)
    {
        lock (_lock)
        {
            return Load(userId).Values.OrderByDescending(e => e.WatchedDate).ThenByDescending(e => e.UpdatedUtc).ToList();
        }
    }

    /// <summary>Devuelve la entrada de una película, o null.</summary>
    /// <param name="userId">Usuario.</param>
    /// <param name="itemId">Película.</param>
    /// <returns>Entrada o null.</returns>
    public DiaryEntry? Get(Guid userId, Guid itemId)
    {
        lock (_lock)
        {
            return Load(userId).TryGetValue(itemId, out var e) ? e : null;
        }
    }

    /// <summary>Crea o actualiza una entrada. Si queda vacía, la borra.</summary>
    /// <param name="userId">Usuario.</param>
    /// <param name="entry">Entrada.</param>
    /// <returns>La entrada guardada, o null si se borró.</returns>
    public DiaryEntry? Save(Guid userId, DiaryEntry entry)
    {
        lock (_lock)
        {
            var all = Load(userId);
            entry.UpdatedUtc = DateTime.UtcNow;
            bool empty = entry.Rating is null && string.IsNullOrWhiteSpace(entry.Notes) && string.IsNullOrEmpty(entry.WatchedDate) && !entry.Liked;
            if (empty)
            {
                all.Remove(entry.ItemId);
            }
            else
            {
                all[entry.ItemId] = entry;
            }

            Persist(userId, all);
            return empty ? null : entry;
        }
    }

    /// <summary>Borra la entrada de una película.</summary>
    /// <param name="userId">Usuario.</param>
    /// <param name="itemId">Película.</param>
    public void Delete(Guid userId, Guid itemId)
    {
        lock (_lock)
        {
            var all = Load(userId);
            if (all.Remove(itemId))
            {
                Persist(userId, all);
            }
        }
    }

    private Dictionary<Guid, DiaryEntry> Load(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var file = FileFor(userId);
        var dict = new Dictionary<Guid, DiaryEntry>();
        if (File.Exists(file))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<DiaryEntry>>(File.ReadAllText(file)) ?? new List<DiaryEntry>();
                foreach (var e in list)
                {
                    dict[e.ItemId] = e;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Film Diary: no se pudo leer {File}; se conserva una copia .bad", file);
                File.Copy(file, file + ".bad", true);
            }
        }

        _cache[userId] = dict;
        return dict;
    }

    private void Persist(Guid userId, Dictionary<Guid, DiaryEntry> all)
    {
        var file = FileFor(userId);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(all.Values.ToList(), JsonOptions));
        File.Move(tmp, file, true);
    }

    private string FileFor(Guid userId) => Path.Combine(_folder, userId.ToString("N") + ".json");
}
