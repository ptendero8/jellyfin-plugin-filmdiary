using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.FilmDiary.Web;

/// <summary>
/// Añade &lt;script src="../FilmDiary/client.js"&gt; al index.html del cliente web, sin tocar ficheros en disco.
/// </summary>
public class ScriptInjectionStartupFilter : IStartupFilter
{
    private const string Marker = "<!-- film-diary -->";
    private const string Tag = Marker + "<script src=\"../FilmDiary/client.js\" defer></script>";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(InjectAsync);
            next(app);
        };
    }

    private static bool IsIndex(PathString path)
    {
        var p = path.Value ?? string.Empty;
        return p.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InjectAsync(HttpContext ctx, Func<Task> next)
    {
        if (!HttpMethods.IsGet(ctx.Request.Method) || !IsIndex(ctx.Request.Path)
            || Plugin.Instance?.Configuration.InjectWebClient == false)
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Pedimos la versión sin comprimir y sin caché condicional para poder modificarla.
        ctx.Request.Headers.Remove("Accept-Encoding");
        ctx.Request.Headers.Remove("If-None-Match");
        ctx.Request.Headers.Remove("If-Modified-Since");

        var original = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            ctx.Response.Body = original;
        }

        buffer.Position = 0;
        var type = ctx.Response.ContentType ?? string.Empty;
        if (ctx.Response.StatusCode == 200 && type.Contains("text/html", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(ctx.Response.Headers.ContentEncoding))
        {
            var html = Encoding.UTF8.GetString(buffer.ToArray());
            if (!html.Contains(Marker, StringComparison.Ordinal))
            {
                var i = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                html = i >= 0 ? html.Insert(i, Tag) : html + Tag;
            }

            var bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.Headers.Remove("ETag");
            ctx.Response.Headers.Remove("Last-Modified");
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.ContentLength = bytes.Length;
            await original.WriteAsync(bytes).ConfigureAwait(false);
            return;
        }

        if (buffer.Length > 0)
        {
            await buffer.CopyToAsync(original).ConfigureAwait(false);
        }
    }
}
