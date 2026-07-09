using Jellyfin.Plugin.Trombee.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Trombee;

/// <summary>
/// Registers plugin services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ActorsIndexService>();
        serviceCollection.AddHostedService<PluginPagesRegistrationService>();

        // Note: the IChannel registration for ActorsIndexChannel has been intentionally
        // removed. Jellyfin's native Channel browsing UI cannot be restyled by the plugin,
        // so Trombee relies solely on its own custom page (actorsBrowse.html) instead.
    }
}
