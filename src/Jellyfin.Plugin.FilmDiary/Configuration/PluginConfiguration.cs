using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FilmDiary.Configuration;

/// <summary>
/// Configuración del plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the web client script is injected automatically into index.html.
    /// </summary>
    public bool InjectWebClient { get; set; } = true;
}
