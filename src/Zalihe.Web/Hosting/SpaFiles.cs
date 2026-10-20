using Microsoft.Net.Http.Headers;

namespace Zalihe.Web.Hosting;

/// <summary>How the React build is cached by browsers.</summary>
public static class SpaFiles
{
    /// <summary>
    /// Vite puts a hash of the content in every file name under /assets, so a file there never changes
    /// and can be kept for a year. Other files (favicon…) are checked again each time.
    /// </summary>
    public static readonly StaticFileOptions Assets = new()
    {
        OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] =
            context.Context.Request.Path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache",
    };

    /// <summary>index.html names the current assets, so after a deployment the browser must get the new one.</summary>
    public static readonly StaticFileOptions IndexPage = new()
    {
        OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
    };
}
