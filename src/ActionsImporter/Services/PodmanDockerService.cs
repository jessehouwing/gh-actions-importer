using ActionsImporter.Interfaces;

namespace ActionsImporter.Services;

public class PodmanDockerService : DockerService
{
    protected override string ContainerCli => "podman";
    protected override string VerifyRunningCommand => "version";
    protected override string VerifyRunningErrorMessage => "Please ensure podman is installed and available";

    public PodmanDockerService(IProcessService processService, IRuntimeService runtimeService)
        : base(processService, runtimeService)
    {
    }

    protected override string? GetContainerArgs()
    {
        return Environment.GetEnvironmentVariable("CONTAINER_ARGS")
            ?? Environment.GetEnvironmentVariable("PODMAN_ARGS")
            ?? Environment.GetEnvironmentVariable("DOCKER_ARGS");
    }
}
