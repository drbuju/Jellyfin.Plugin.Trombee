using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Trombee.Configuration;
using Jellyfin.Plugin.Trombee.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Trombee.Api;

/// <summary>
/// Simple API for Actors Index.
/// </summary>
[ApiController]
[Route("Trombee")]
public class ActorsIndexController : ControllerBase
{
    private const string PluginManifestUrl = "https://raw.githubusercontent.com/drbuju/Jellyfin.Plugin.Trombee/main/manifest.json";

    private readonly ActorsIndexService _actorsIndexService;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorsIndexController"/> class.
    /// </summary>
    /// <param name="actorsIndexService">The actors index service.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="providerManager">The provider manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public ActorsIndexController(
        ActorsIndexService actorsIndexService,
        ILibraryManager libraryManager,
        IUserManager userManager,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        IHttpClientFactory httpClientFactory)
    {
        _actorsIndexService = actorsIndexService;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Returns a simple ping response.
    /// </summary>
    /// <returns>A simple JSON payload.</returns>
    [HttpGet("ping")]
    public ActionResult<object> Ping()
    {
        return Ok(new
        {
            status = "ok",
            plugin = "Trombee"
        });
    }

    /// <summary>
    /// Returns the current plugin configuration.
    /// </summary>
    /// <returns>The current configuration values.</returns>
    [HttpGet("config")]
    public ActionResult<object> GetConfig()
    {
        PluginConfiguration config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        return Ok(new
        {
            enabled = config.Enabled,
            minimumAppearances = config.MinimumAppearances,
            showRoleName = config.ShowRoleName
        });
    }

    /// <summary>
    /// Returns the current service status.
    /// </summary>
    /// <returns>A test payload from the service.</returns>
    [HttpGet("service-status")]
    public ActionResult<object> GetServiceStatus()
    {
        return Ok(_actorsIndexService.GetStatus());
    }

    /// <summary>
    /// Serves the actors browse page as plain HTML, reachable by any authenticated user
    /// (not just administrators). Used as the target URL registered with the Plugin Pages
    /// plugin, so that regular users can access it outside the admin Dashboard.
    /// </summary>
    /// <returns>The browse page HTML.</returns>
    [HttpGet("Pages/Browse")]
    public ActionResult GetBrowsePage()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        const string resourceName = "Jellyfin.Plugin.Trombee.Configuration.actorsBrowse.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound("Browse page resource not found.");
        }

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();
        return Content(html, "text/html");
    }

    /// <summary>
    /// Returns the actors index with appearance counts.
    /// </summary>
    /// <param name="personType">The type of person to include (e.g. Actor, Director, Writer). Defaults to Actor.</param>
    /// <returns>Sorted list of people with item occurrences.</returns>
    [HttpGet("actors-index")]
    public ActionResult<object> GetActorsIndex([FromQuery] string? personType = null)
    {
        var userId = User.GetUserId();
        var callingUser = userId != Guid.Empty ? _userManager.GetUserById(userId) : null;

        var personKind = Jellyfin.Data.Enums.PersonKind.Actor;
        if (!string.IsNullOrEmpty(personType))
        {
            _ = Enum.TryParse(personType, ignoreCase: true, out personKind);
        }

        return Ok(_actorsIndexService.GetActorsIndex(callingUser, personKind));
    }

    /// <summary>
    /// Returns simple library statistics.
    /// </summary>
    /// <returns>Library counts.</returns>
    [HttpGet("library-stats")]
    public ActionResult<object> GetLibraryStats()
    {
        return Ok(_actorsIndexService.GetLibraryStats());
    }

    /// <summary>
    /// Returns a Jellyfin plugin repository manifest for this plugin.
    /// Register the URL of this endpoint as a custom repository in Jellyfin
    /// to suppress the "PluginLoadRepoError" warning on the plugin details page.
    /// </summary>
    /// <returns>A valid Jellyfin plugin repository manifest.</returns>
    [HttpGet("repository")]
    public ActionResult GetRepository()
    {
        var plugin = Plugin.Instance;
        var version = plugin?.Version.ToString() ?? "1.0.0.0";
        var guid = plugin?.Id.ToString() ?? "c6d7356a-44b3-4a91-a86c-932d633b51b1";

        var manifest = new[]
        {
            new
            {
                category = "General",
                description = "Scans your Jellyfin library and builds an index of all actors with photos, search and pagination.",
                guid,
                imageUrl = (string?)null,
                name = "Trombee",
                overview = "Browse all actors in your library with appearance counts.",
                owner = "drbuju",
                versions = new[]
                {
                    new
                    {
                        checksum = (string?)null,
                        changelog = "1.0.0 - Initial release.",
                        targetAbi = "10.11.0.0",
                        sourceUrl = (string?)null,
                        timestamp = "2026-04-04T00:00:00Z",
                        version
                    }
                }
            }
        };

        return new JsonResult(manifest);
    }

    /// <summary>
    /// Queues a full image metadata refresh for all Person items in the library.
    /// Use this to fix actor cards showing wrong images (e.g. movie posters).
    /// </summary>
    /// <returns>A summary of the operation.</returns>
    [HttpPost("refresh-people")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult<object> RefreshPeople()
    {
        var people = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
        {
            IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Person },
            Recursive = true,
        });

        var refreshOpts = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
        {
            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            ReplaceAllImages = true,
            ReplaceAllMetadata = true,
        };

        foreach (var person in people)
        {
            _providerManager.QueueRefresh(person.Id, refreshOpts, RefreshPriority.Normal);
        }

        return Ok(new
        {
            peopleQueued = people.Count,
        });
    }

    // ── Self-update ────────────────────────────────────────────────────────

    /// <summary>
    /// Checks GitHub for a newer version of this plugin.
    /// </summary>
    /// <returns>Version comparison result.</returns>
    [HttpGet("check-update")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult> CheckUpdate()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var json = await client.GetStringAsync(new Uri(PluginManifestUrl)).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var versionsArray = doc.RootElement[0].GetProperty("versions");

            Version? latestVersion = null;
            string? latestVersionStr = null;

            foreach (var entry in versionsArray.EnumerateArray())
            {
                var verStr = entry.GetProperty("version").GetString() ?? string.Empty;
                if (Version.TryParse(verStr, out var parsed) && (latestVersion is null || parsed > latestVersion))
                {
                    latestVersion = parsed;
                    latestVersionStr = verStr;
                }
            }

            var current = Plugin.Instance?.Version ?? new Version(1, 0, 0, 0);
            return Ok(new
            {
                currentVersion = current.ToString(),
                latestVersion = latestVersionStr,
                updateAvailable = latestVersion is not null && latestVersion > current,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Downloads the latest plugin release from GitHub and installs it in-place.
    /// Jellyfin must be restarted after this operation to load the new DLL.
    /// </summary>
    /// <returns>Update result.</returns>
    [HttpPost("self-update")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult> SelfUpdate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "actorsindex_upd_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var client = _httpClientFactory.CreateClient();

            // 1. Fetch manifest from GitHub
            var json = await client.GetStringAsync(new Uri(PluginManifestUrl)).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var versionsArray = doc.RootElement[0].GetProperty("versions");

            Version? latestVersion = null;
            string? sourceUrl = null;
            string? checksum = null;

            foreach (var entry in versionsArray.EnumerateArray())
            {
                var verStr = entry.GetProperty("version").GetString() ?? string.Empty;
                if (Version.TryParse(verStr, out var parsed) && (latestVersion is null || parsed > latestVersion))
                {
                    latestVersion = parsed;
                    sourceUrl = entry.TryGetProperty("sourceUrl", out var su) ? su.GetString() : null;
                    checksum = entry.TryGetProperty("checksum", out var cs) ? cs.GetString() : null;
                }
            }

            var current = Plugin.Instance?.Version ?? new Version(1, 0, 0, 0);
            if (latestVersion is null || latestVersion <= current)
            {
                return Ok(new { status = "up_to_date", version = current.ToString() });
            }

            if (string.IsNullOrEmpty(sourceUrl))
            {
                return StatusCode(500, new { error = "sourceUrl non presente nel manifest GitHub." });
            }

            // 2. Download ZIP
            Directory.CreateDirectory(tempDir);
            var zipBytes = await client.GetByteArrayAsync(new Uri(sourceUrl)).ConfigureAwait(false);

            // 3. Verify MD5 checksum (MD5 used only for integrity, not security)
#pragma warning disable CA5351
            if (!string.IsNullOrEmpty(checksum))
            {
                var hash = Convert.ToHexString(MD5.HashData(zipBytes)).ToLowerInvariant();
                if (!string.Equals(hash, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(500, new { error = $"Checksum MD5 non valido. Atteso: {checksum}, calcolato: {hash}" });
                }
            }
#pragma warning restore CA5351

            var zipPath = Path.Combine(tempDir, "update.zip");
            await System.IO.File.WriteAllBytesAsync(zipPath, zipBytes).ConfigureAwait(false);

            // 4. Extract ZIP
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

            // 5. Copy .dll / .json / .png to the plugin directory
            var pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!;
            var copied = new List<string>();

            foreach (var filePath in Directory.GetFiles(tempDir))
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is ".dll" or ".json" or ".png")
                {
                    var dest = Path.Combine(pluginDir, Path.GetFileName(filePath));
                    System.IO.File.Copy(filePath, dest, overwrite: true);
                    copied.Add(Path.GetFileName(filePath));
                }
            }

            return Ok(new
            {
                status = "updated",
                newVersion = latestVersion.ToString(),
                files = copied,
                restartRequired = true,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
