using System.Text.Json;
using ActionsImporter.Interfaces;
using ActionsImporter.Models;
using ActionsImporter.Models.Docker;

namespace ActionsImporter.Services;

public class DockerService : IDockerService
{
    protected IProcessService ProcessService { get; }
    protected IRuntimeService RuntimeService { get; }
    protected virtual string ContainerCli => "docker";
    protected virtual string VerifyRunningCommand => "info";
    protected virtual string VerifyRunningErrorMessage => "Please ensure docker is installed and the docker daemon is running";

    public DockerService(IProcessService processService, IRuntimeService runtimeService)
    {
        ArgumentNullException.ThrowIfNull(processService);
        ArgumentNullException.ThrowIfNull(runtimeService);
        ProcessService = processService;
        RuntimeService = runtimeService;
    }

    public Task UpdateImageAsync(string image, string server, string version)
    {
        return PullImageAsync(image, server, version);
    }

    public async Task ExecuteCommandAsync(string image, string server, string version, bool noHostNetwork, params string[] arguments)
    {
        var actionsImporterArguments = new List<string>
        {
            "run --rm -t"
        };

        if (UseHostNetwork(noHostNetwork))
        {
            actionsImporterArguments.Add("--network=host");
        }

        actionsImporterArguments.AddRange(GetEnvironmentVariableArguments());

        var containerArgs = GetContainerArgs();
        if (containerArgs is not null)
        {
            actionsImporterArguments.Add(containerArgs);
        }

        if (RuntimeService.IsLinux)
        {
            var (userId, _, _) = await ProcessService.RunAndCaptureAsync("id", "-u");
            var (groupId, _, _) = await ProcessService.RunAndCaptureAsync("id", "-g");
            actionsImporterArguments.Add($"-e USER_ID={userId.TrimEnd()}");
            actionsImporterArguments.Add($"-e GROUP_ID={groupId.TrimEnd()}");
        }

        actionsImporterArguments.Add($"-v \"{GetVolumePath(Directory.GetCurrentDirectory())}\":/data");
        actionsImporterArguments.Add($"{server}/{image}:{version}");
        actionsImporterArguments.AddRange(arguments);

        await ProcessService.RunAsync(
            ContainerCli,
            string.Join(' ', actionsImporterArguments),
            Directory.GetCurrentDirectory(),
            new[] { ("MSYS_NO_PATHCONV", "1") }
        );
    }

    public async Task<List<Feature>> GetFeaturesAsync(string image, string server, string version)
    {
        var actionsImporterArguments = new List<string> { "run --rm -t" };
        actionsImporterArguments.AddRange(GetEnvironmentVariableArguments());

        var containerArgs = GetContainerArgs();
        if (containerArgs is not null)
        {
            actionsImporterArguments.Add(containerArgs);
        }

        actionsImporterArguments.Add($"{server}/{image}:{version}");
        actionsImporterArguments.AddRange(new[] { "list-features", "--json" });

        var (standardOutput, _, _) = await ProcessService.RunAndCaptureAsync(ContainerCli, string.Join(' ', actionsImporterArguments), throwOnError: false);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, };
        try
        {
            return JsonSerializer.Deserialize<List<Feature>>(standardOutput, options) ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public async Task VerifyDockerRunningAsync()
    {
        try
        {
            await ProcessService.RunAsync(
                ContainerCli,
                VerifyRunningCommand,
                output: false
            );
        }
        catch (Exception)
        {
            throw new Exception(VerifyRunningErrorMessage);
        }
    }

    public async Task VerifyImagePresentAsync(string image, string server, string version, bool isPrerelease)
    {
        var imageName = $"{server}/{image}:{version}";
        var preReleaseOption = isPrerelease ? " --prerelease" : string.Empty;
        try
        {
            await ProcessService.RunAsync(
                ContainerCli,
                $"image inspect {server}/{image}:{version}",
                output: false
            );
        }
        catch (Exception)
        {
            throw new Exception($"Unable to locate {imageName} image locally. Please run `gh actions-importer update{preReleaseOption}` to fetch the latest image prior to running this command.");
        }
    }

    public virtual async Task<string?> GetLatestImageDigestAsync(string image, string server)
    {
        var (standardOutput, _, _) = await ProcessService.RunAndCaptureAsync(ContainerCli, $"manifest inspect {server}/{image}");
        Manifest? manifest = JsonSerializer.Deserialize<Manifest>(standardOutput);

        return manifest?.GetDigest();
    }

    public virtual async Task<string?> GetCurrentImageDigestAsync(string image, string server)
    {
        var (standardOutput, _, _) = await ProcessService.RunAndCaptureAsync(ContainerCli, $"image inspect --format={{{{.Id}}}} {server}/{image}");

        return standardOutput.Split(":").ElementAtOrDefault(1)?.Trim();
    }

    protected virtual bool UseHostNetwork(bool noHostNetwork)
    {
        return !noHostNetwork;
    }

    protected virtual string? GetContainerArgs()
    {
        return Environment.GetEnvironmentVariable("CONTAINER_ARGS")
            ?? Environment.GetEnvironmentVariable("DOCKER_ARGS");
    }

    protected virtual string GetVolumePath(string path)
    {
        return path;
    }

    protected static string? GetDigestFromImageInspect(string standardOutput)
    {
        using var document = JsonDocument.Parse(standardOutput);

        var root = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().FirstOrDefault()
            : document.RootElement;

        if (root.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (TryGetPropertyIgnoreCase(root, "RepoDigests", out var repoDigests) &&
            repoDigests.ValueKind == JsonValueKind.Array)
        {
            foreach (var repoDigest in repoDigests.EnumerateArray())
            {
                var digest = repoDigest.GetString()?.Split('@').ElementAtOrDefault(1);
                if (!string.IsNullOrWhiteSpace(digest))
                {
                    return digest.Split(':').ElementAtOrDefault(1)?.Trim();
                }
            }
        }

        return TryGetPropertyIgnoreCase(root, "Id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()?.Split(':').ElementAtOrDefault(1)?.Trim()
            : null;
    }

    private static IEnumerable<string> GetEnvironmentVariableArguments()
    {
        if (File.Exists(".env.local"))
        {
            yield return "--env-file .env.local";
        }

        foreach (var env in Constants.EnvironmentVariables)
        {
            var value = Environment.GetEnvironmentVariable(env);

            if (string.IsNullOrWhiteSpace(value)) continue;

            var key = env;
            if (key.StartsWith("GH_", StringComparison.Ordinal))
                key = key.Replace("GH_", "GITHUB_", StringComparison.Ordinal);

            yield return $"--env \"{key}={value}\"";
        }
    }

    private async Task PullImageAsync(string image, string server, string version)
    {
        Console.WriteLine($"Updating {server}/{image}:{version}...");
        var (_, standardError, exitCode) = await ProcessService.RunAndCaptureAsync(
            ContainerCli,
            GetPullArguments(image, server, version),
            throwOnError: false
        );

        if (exitCode != 0)
        {
            string message = standardError.Trim();
            string errorMessage = $"There was an error pulling the {server}/{image}:{version}.\nError: {message}";

            throw new Exception(errorMessage);
        }
        Console.WriteLine($"{server}/{image}:{version} up-to-date");
    }

    protected virtual string GetPullArguments(string image, string server, string version)
    {
        return $"pull {server}/{image}:{version} --quiet";
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
