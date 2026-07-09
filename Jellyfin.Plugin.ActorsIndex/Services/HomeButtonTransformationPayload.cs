using Newtonsoft.Json;

namespace Jellyfin.Plugin.Trombee.Services;

/// <summary>
/// Payload passed by the File Transformation plugin to a registered callback.
/// </summary>
public class HomeButtonTransformationPayload
{
    /// <summary>
    /// Gets or sets the current contents of the file being served.
    /// </summary>
    [JsonProperty("contents")]
    public string? Contents { get; set; }
}
