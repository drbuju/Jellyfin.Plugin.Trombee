using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Trombee.Services;

/// <summary>
/// Provides Actors Index data access.
/// </summary>
public class ActorsIndexService
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorsIndexService"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public ActorsIndexService(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Returns whether the service is ready.
    /// </summary>
    /// <returns>A test payload.</returns>
    public object GetStatus()
    {
        return new
        {
            status = "ok",
            libraryManagerAvailable = _libraryManager is not null
        };
    }

    /// <summary>
    /// Returns the top-level libraries (folders) visible to the given user, respecting
    /// their library access permissions. Pass <c>null</c> to return every library.
    /// </summary>
    /// <param name="user">The user to scope the results to, or <c>null</c> for no scoping.</param>
    /// <returns>A list of libraries with their ID and name.</returns>
    public object GetLibraries(Jellyfin.Database.Implementations.Entities.User? user = null)
    {
        var folders = user is not null
            ? _libraryManager.GetUserRootFolder().GetChildren(user, true)
            : _libraryManager.GetUserRootFolder().Children;

        var libraries = folders
            .Where(f => f is Folder)
            .Select(f => new
            {
                id = f.Id.ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                name = f.Name
            })
            .OrderBy(f => f.name, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new { libraries };
    }

    /// <summary>
    /// Returns the actors index: each person of the given type with their appearance count
    /// and related items, scoped to the libraries the given user has access to. Pass
    /// <c>null</c> user to include everything (used internally / by administrators).
    /// </summary>
    /// <param name="user">The user to scope the results to, or <c>null</c> for no scoping.</param>
    /// <param name="personKind">The type of person to include (defaults to Actor).</param>
    /// <param name="libraryIds">If provided, restrict results to these top-level library (folder) IDs.</param>
    /// <returns>A sorted list of people with occurrence counts.</returns>
    public object GetActorsIndex(Jellyfin.Database.Implementations.Entities.User? user = null, PersonKind personKind = PersonKind.Actor, System.Guid[]? libraryIds = null)
    {
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

        // Get all movies and series from the library, scoped to what this user can see
        // (respects library access permissions and parental controls), and optionally
        // restricted to a specific set of libraries the caller selected.
        System.Collections.Generic.IReadOnlyList<BaseItem> items;
        if (libraryIds is { Length: > 0 })
        {
            // Query each selected library individually (via ParentId) and merge, rather than
            // relying on TopParentIds — more reliable across Jellyfin versions.
            var merged = new System.Collections.Generic.Dictionary<System.Guid, BaseItem>();
            foreach (var libraryId in libraryIds)
            {
                var libraryItems = _libraryManager.GetItemList(new InternalItemsQuery(user)
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode },
                    Recursive = true,
                    ParentId = libraryId
                });

                foreach (var item in libraryItems)
                {
                    merged[item.Id] = item;
                }
            }

            items = merged.Values.ToList();
        }
        else
        {
            items = _libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode },
                Recursive = true
            });
        }

        // Build actor-to-items mapping by querying people for each item. Keyed by item ID
        // per actor (not a plain list) so that multiple episodes of the same series only
        // ever produce a single "Series" entry, instead of one per episode.
        var actorDict = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<System.Guid, (System.Guid ItemId, string ItemName, string? Role, int? Year, string ItemType)>>(
            System.StringComparer.OrdinalIgnoreCase);

        // "Actor" includes "Guest Star" credits too, since both represent on-screen
        // appearances from a user's point of view (guest stars are common on TV series).
        var matchesRequestedType = personKind == PersonKind.Actor
            ? (System.Func<PersonKind, bool>)(t => t == PersonKind.Actor || t == PersonKind.GuestStar)
            : t => t == personKind;

        // Cache resolved series (id -> name/year) so repeated episodes of the same show
        // don't each trigger a separate lookup.
        var seriesCache = new System.Collections.Generic.Dictionary<System.Guid, (string Name, int? Year)>();

        foreach (var item in items)
        {
            System.Guid effectiveId;
            string effectiveName;
            int? effectiveYear;
            string effectiveType;

            if (item is Episode episode && episode.SeriesId != System.Guid.Empty)
            {
                effectiveId = episode.SeriesId;
                effectiveType = "Series";

                if (!seriesCache.TryGetValue(episode.SeriesId, out var seriesInfo))
                {
                    var seriesItem = _libraryManager.GetItemById(episode.SeriesId);
                    seriesInfo = (seriesItem?.Name ?? episode.SeriesName ?? item.Name, seriesItem?.ProductionYear);
                    seriesCache[episode.SeriesId] = seriesInfo;
                }

                effectiveName = seriesInfo.Name;
                effectiveYear = seriesInfo.Year;
            }
            else
            {
                effectiveId = item.Id;
                effectiveName = item.Name;
                effectiveYear = item.ProductionYear;
                effectiveType = item.GetType().Name;
            }

            foreach (var person in _libraryManager.GetPeople(item))
            {
                if (!matchesRequestedType(person.Type) || person.Name is null)
                {
                    continue;
                }

                if (!actorDict.TryGetValue(person.Name, out var appearances))
                {
                    appearances = new System.Collections.Generic.Dictionary<System.Guid, (System.Guid, string, string?, int?, string)>();
                    actorDict[person.Name] = appearances;
                }

                appearances[effectiveId] = (effectiveId, effectiveName, person.Role, effectiveYear, effectiveType);
            }
        }

        // Resolve person item IDs for thumbnail images (one bulk query)
        var personIdByName = _libraryManager
            .GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.Person }, Recursive = true })
            .Where(p => p.Name is not null)
            .GroupBy(p => p.Name!, System.StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id.ToString(), System.StringComparer.OrdinalIgnoreCase);

        var actors = actorDict
            .Select(kvp =>
            {
                personIdByName.TryGetValue(kvp.Key, out var personId);
                return new
                {
                    name = kvp.Key,
                    appearances = kvp.Value.Count,
                    personId,
                    items = kvp.Value.Values
                        .Select(x => new { itemId = x.ItemId, itemName = x.ItemName, role = x.Role, year = x.Year, itemType = x.ItemType })
                        .ToArray()
                };
            })
            .Where(a => a.appearances >= config.MinimumAppearances)
            .OrderByDescending(a => a.appearances)
            .ThenBy(a => a.name)
            .ToArray();

        return new
        {
            status = "ok",
            totalActors = actors.Length,
            actors
        };
    }

    /// <summary>
    /// Returns simple library statistics.
    /// </summary>
    /// <returns>Counts of root children in the Jellyfin library.</returns>
    public object GetLibraryStats()
    {
        var rootFolder = _libraryManager.RootFolder;
        var directChildren = rootFolder.Children.ToList();

        int libraryCount = directChildren.Count(item => item.IsFolder);
        int movieCount = directChildren.Count(item => item is Movie);
        int seriesCount = directChildren.Count(item => item is Series);

        return new
        {
            status = "ok",
            rootFolderName = rootFolder.Name,
            totalDirectChildren = directChildren.Count,
            libraryFolders = libraryCount,
            movieCount,
            seriesCount,
            sampleChildren = directChildren.Take(5).Select(i => new
            {
                i.Id,
                i.Name,
                itemType = i.GetType().Name
            }).ToArray()
        };
    }
}
