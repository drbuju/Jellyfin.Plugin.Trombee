using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Trombee.Services;

/// <summary>
/// Registers the Home-button injection as a File Transformation (if that plugin is installed),
/// so the button is patched into <c>index.html</c> at serve time instead of being written to
/// disk. This avoids file-permission issues and survives Jellyfin updates automatically.
/// </summary>
public class FileTransformationRegistrationService : IHostedService
{
    private readonly ILogger<FileTransformationRegistrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransformationRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{FileTransformationRegistrationService}"/> interface.</param>
    public FileTransformationRegistrationService(ILogger<FileTransformationRegistrationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        RegisterTransformation();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void RegisterTransformation()
    {
        try
        {
            var fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(x => x.Assemblies)
                .FirstOrDefault(x => x.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false);

            if (fileTransformationAssembly is null)
            {
                _logger.LogInformation(
                    "File Transformation plugin not found. The Home button can still be injected manually from the Trombee settings page.");
                return;
            }

            var pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            if (pluginInterfaceType is null)
            {
                _logger.LogWarning("Could not find File Transformation's PluginInterface type. Skipping registration.");
                return;
            }

            var registerMethod = pluginInterfaceType.GetMethod("RegisterTransformation");
            if (registerMethod is null)
            {
                _logger.LogWarning("Could not find RegisterTransformation method. Skipping registration.");
                return;
            }

            var payload = new JObject
            {
                ["id"] = "c6d7356a-44b3-4a91-a86c-932d633b51b2",
                ["fileNamePattern"] = "(^|/)index\\.html$",
                ["callbackAssembly"] = typeof(FileTransformationRegistrationService).Assembly.FullName,
                ["callbackClass"] = typeof(HomeButtonTransformationPatch).FullName,
                ["callbackMethod"] = nameof(HomeButtonTransformationPatch.InjectButton)
            };

            registerMethod.Invoke(null, new object?[] { payload });
            _logger.LogInformation("Trombee Home button transformation registered with File Transformation successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering Trombee's Home button transformation with File Transformation.");
        }
    }
}
