using System;
using System.Collections.Generic;
using Jellyfin.Plugin.FilmDiary.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.FilmDiary;

/// <summary>
/// Film Diary: puntuaciones de 0 a 5 estrellas (con medias), notas y diario de visionado por usuario.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>Identificador fijo del plugin.</summary>
    public static readonly Guid PluginId = new("6f1c2b0e-8a4d-4f3a-9c55-2d7e0b9a41f7");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Rutas de la aplicación.</param>
    /// <param name="xmlSerializer">Serializador XML.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>Instancia actual.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Film Diary";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public override string Description => "Puntúa tus películas de 0 a 5 estrellas (con medias), escribe notas y lleva un diario de lo que ves. Exporta a CSV compatible con Letterboxd.";
}
