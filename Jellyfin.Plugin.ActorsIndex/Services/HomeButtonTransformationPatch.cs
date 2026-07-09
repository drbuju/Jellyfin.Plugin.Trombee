using System;

namespace Jellyfin.Plugin.Trombee.Services;

/// <summary>
/// Contains the callback invoked by File Transformation to patch <c>index.html</c> at serve time.
/// </summary>
public static class HomeButtonTransformationPatch
{
    private const string InjectionTag = "\n    <!-- Trombee --><script src=\"/Trombee/ui-button.js\"></script><!-- /Trombee -->";

    /// <summary>
    /// Injects the floating Home button script tag into the served <c>index.html</c>, unless
    /// it has already been injected (e.g. by the manual disk-based injection button).
    /// </summary>
    /// <param name="payload">The file contents payload provided by File Transformation.</param>
    /// <returns>The patched HTML.</returns>
    public static string InjectButton(HomeButtonTransformationPayload payload)
    {
        var html = payload.Contents ?? string.Empty;

        if (html.Contains("<!-- Trombee -->", StringComparison.Ordinal))
        {
            return html;
        }

        var patched = html.Replace("</body>", InjectionTag + "\n</body>", StringComparison.Ordinal);
        return patched;
    }
}
