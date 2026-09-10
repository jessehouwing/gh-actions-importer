using System.Collections.Immutable;
using ActionsImporter.Interfaces;
using ActionsImporter.Services;

namespace ActionsImporter;

public static class ContainerConfiguration
{
    private const string ContainerCliKey = "CONTAINER_CLI";

    public static string GetContainerCli(ImmutableDictionary<string, string> configurationVariables)
    {
        ArgumentNullException.ThrowIfNull(configurationVariables);

        var containerCli = Environment.GetEnvironmentVariable(ContainerCliKey);
        if (string.IsNullOrWhiteSpace(containerCli) && configurationVariables.TryGetValue(ContainerCliKey, out var configuredContainerCli))
        {
            containerCli = configuredContainerCli;
        }

        return string.IsNullOrWhiteSpace(containerCli) ? "docker" : containerCli.Trim();
    }

    public static IDockerService CreateDockerService(
        IProcessService processService,
        IRuntimeService runtimeService,
        ImmutableDictionary<string, string> configurationVariables)
    {
        return GetContainerCli(configurationVariables).ToUpperInvariant() switch
        {
            "WSLC" => new WslcDockerService(processService, runtimeService),
            "PODMAN" => new PodmanDockerService(processService, runtimeService),
            _ => new DockerService(processService, runtimeService)
        };
    }
}
