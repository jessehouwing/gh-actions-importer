using ActionsImporter.Interfaces;

namespace ActionsImporter.Services;

public class WslcDockerService : DockerService
{
    protected override string ContainerCli => "wslc";
    protected override string VerifyRunningCommand => "version";
    protected override string VerifyRunningErrorMessage => "Please ensure wslc is installed and WSL containers are available";

    public WslcDockerService(IProcessService processService, IRuntimeService runtimeService)
        : base(processService, runtimeService)
    {
    }

    public override Task<string?> GetLatestImageDigestAsync(string image, string server)
    {
        return Task.FromResult<string?>(null);
    }

    public override async Task<string?> GetCurrentImageDigestAsync(string image, string server)
    {
        var (inspectOutput, _, _) = await ProcessService.RunAndCaptureAsync(ContainerCli, $"image inspect {server}/{image}");
        return GetDigestFromImageInspect(inspectOutput);
    }

    protected override bool UseHostNetwork(bool noHostNetwork)
    {
        return false;
    }

    protected override string? GetContainerArgs()
    {
        return Environment.GetEnvironmentVariable("CONTAINER_ARGS")
            ?? Environment.GetEnvironmentVariable("WSLC_ARGS")
            ?? Environment.GetEnvironmentVariable("DOCKER_ARGS");
    }

    protected override string GetVolumePath(string path)
    {
        return path.Replace('\\', '/');
    }

    protected override string GetPullArguments(string image, string server, string version)
    {
        return $"pull {server}/{image}:{version}";
    }
}
