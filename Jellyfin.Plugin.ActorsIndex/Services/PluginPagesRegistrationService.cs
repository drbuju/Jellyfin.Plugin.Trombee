using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Trombee.Services;

/// <summary>
/// Registers the Trombee browse page with the Plugin Pages plugin (if installed), so that
/// regular (non-administrator) users can reach it outside the admin Dashboard. Uses reflection
/// rather than a direct assembly/NuGet reference because Jellyfin loads each plugin into its
/// own isolated AssemblyLoadContext.
/// </summary>
public class PluginPagesRegistrationService : IHostedService
{
    private readonly ILogger<PluginPagesRegistrationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginPagesRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PluginPagesRegistrationService}"/> interface.</param>
    /// <param name="serviceProvider">Instance of the <see cref="IServiceProvider"/> interface.</param>
    public PluginPagesRegistrationService(ILogger<PluginPagesRegistrationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        RegisterPage();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void RegisterPage()
    {
        try
        {
            // Locate the Plugin Pages assembly among all currently loaded plugin contexts.
            var pluginPagesAssembly = AssemblyLoadContext.All
                .SelectMany(x => x.Assemblies)
                .FirstOrDefault(x => x.FullName?.Contains("Jellyfin.Plugin.PluginPages", StringComparison.Ordinal) ?? false);

            if (pluginPagesAssembly is null)
            {
                _logger.LogInformation(
                    "Plugin Pages plugin not found. The Trombee browse page will still be reachable directly at /Trombee/Pages/Browse for authenticated users, but won't appear as a menu item outside the admin Dashboard.");
                return;
            }

            var managerInterfaceType = pluginPagesAssembly.GetType("Jellyfin.Plugin.PluginPages.Library.IPluginPagesManager");
            var pluginPageType = pluginPagesAssembly.GetType("Jellyfin.Plugin.PluginPages.Library.PluginPage");

            if (managerInterfaceType is null || pluginPageType is null)
            {
                _logger.LogWarning("Could not find Plugin Pages types. Skipping registration.");
                return;
            }

            var getServiceMethod = typeof(ServiceProviderServiceExtensions).GetMethod(
                nameof(ServiceProviderServiceExtensions.GetService),
                new[] { typeof(IServiceProvider) });

            if (getServiceMethod is null)
            {
                _logger.LogWarning("Could not find GetService method.");
                return;
            }

            var genericGetService = getServiceMethod.MakeGenericMethod(managerInterfaceType);
            var manager = genericGetService.Invoke(null, new object[] { _serviceProvider });

            if (manager is null)
            {
                _logger.LogWarning("Plugin Pages manager not found in DI. Skipping registration.");
                return;
            }

            var page = Activator.CreateInstance(pluginPageType);
            if (page is null)
            {
                _logger.LogWarning("Could not create PluginPage instance.");
                return;
            }

            var idProperty = pluginPageType.GetProperty("Id");
            var urlProperty = pluginPageType.GetProperty("Url");
            var displayTextProperty = pluginPageType.GetProperty("DisplayText");
            var iconProperty = pluginPageType.GetProperty("Icon");

            idProperty?.SetValue(page, "trombee-browse");
            urlProperty?.SetValue(page, "/Trombee/Pages/Browse");
            displayTextProperty?.SetValue(page, "Trombee - Browse Actors");
            iconProperty?.SetValue(page, "people");

            var registerMethod = managerInterfaceType.GetMethod("RegisterPluginPage");
            if (registerMethod is not null)
            {
                registerMethod.Invoke(manager, new[] { page });
                _logger.LogInformation("Trombee browse page registered with Plugin Pages successfully.");
            }
            else
            {
                _logger.LogWarning("Could not find RegisterPluginPage method.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering Trombee page with Plugin Pages.");
        }
    }
}
