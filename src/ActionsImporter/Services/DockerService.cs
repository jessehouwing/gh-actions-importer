using System.Collections.Immutable;
using System.Text.Json;
using ActionsImporter.Interfaces;
using ActionsImporter.Models.Docker;
using ActionsImporter.Models;

namespace ActionsImporter.Services;

public class DockerService : IDockerService
{
    private readonly IProcessService _processService;
    private readonly IRuntimeService _runtimeService;
    private readonly string _containerCli;
    private bool IsWslc => _containerCli.Equals("wslc", StringComparison.OrdinalIgnoreCase);

    public DockerService(IProcessService processService, IRuntimeService runtimeService, ImmutableDictionary<string, string> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(processService);
        ArgumentNullException.ThrowIfNull(runtimeService);
        ArgumentNullException.ThrowIfNull(environmentVariables);
        _processService = processService;
        _runtimeService = runtimeService;
        _containerCli = environmentVariables.TryGetValue("CONTAINER_CLI", out var containerCli) && !string.IsNullOrWhiteSpace(containerCli) ? containerCli : "docker";
    }

    public Task UpdateImageAsync(string image, string server, string version)
    {
        return DockerPullAsync(image, server, version);
    }

    public async Task ExecuteCommandAsync(string image, string server, string version, bool noHostNetwork, params string[] arguments)
    {
        var actionsImporterArguments = new List<string>
        {
            "run --rm -t"
        };

        if (ShouldUseHostNetwork(noHostNetwork))
        {
            actionsImporterArguments.Add("--network=host");
        }

        actionsImporterArguments.AddRange(GetEnvironmentVariableArguments());

        var containerArgs = Environment.GetEnvironmentVariable("CONTAINER_ARGS")
            ?? Environment.GetEnvironmentVariable(IsWslc ? "WSLC_ARGS" : "DOCKER_ARGS")
            ?? (IsWslc ? Environment.GetEnvironmentVariable("DOCKER_ARGS") : null);
        if (containerArgs is not null)
        {
            actionsImporterArguments.Add(containerArgs);
        }

        // Forward the current user's UID/GID to the container
        // to ensure the output files are owned by the current user
        if (_runtimeService.IsLinux)
        {
            var (userId, _, _) = await _processService.RunAndCaptureAsync("id", "-u");
            var (groupId, _, _) = await _processService.RunAndCaptureAsync("id", "-g");
            actionsImporterArguments.Add($"-e USER_ID={userId.TrimEnd()}");
            actionsImporterArguments.Add($"-e GROUP_ID={groupId.TrimEnd()}");
        }
        actionsImporterArguments.Add($"-v \"{GetVolumePath(Directory.GetCurrentDirectory())}\":/data");
        actionsImporterArguments.Add($"{server}/{image}:{version}");
        actionsImporterArguments.AddRange(arguments);

        await _processService.RunAsync(
            _containerCli,
            string.Join(' ', actionsImporterArguments),
            Directory.GetCurrentDirectory(),
            new[] { ("MSYS_NO_PATHCONV", "1") }
        );
    }

    public async Task<List<Feature>> GetFeaturesAsync(string image, string server, string version)
    {
        var actionsImporterArguments = new List<string> { "run --rm -t" };
        actionsImporterArguments.AddRange(GetEnvironmentVariableArguments());
        actionsImporterArguments.Add($"{server}/{image}:{version}");
        actionsImporterArguments.AddRange(new[] { "list-features", "--json" });

        var (standardOutput, _, _) = await _processService.RunAndCaptureAsync(_containerCli, string.Join(' ', actionsImporterArguments), throwOnError: false);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, };
        try
        {
            return JsonSerializer.Deserialize<List<Feature>>(standardOutput, options) ?? new();
        }
        catch (Exception)
        {
            // If unable to get the features from the container, return an empty list
            // An empty list will result in a message being displayed to the user
            return new();
        }
    }

    public async Task VerifyDockerRunningAsync()
    {
        try
        {
            await _processService.RunAsync(
                _containerCli,
                IsWslc ? "version" : "info",
                output: false
            );
        }
        catch (Exception)
        {
            throw new Exception(IsWslc
                ? "Please ensure wslc is installed and WSL containers are available"
                : "Please ensure docker is installed and the docker daemon is running");
        }
    }

    public async Task VerifyImagePresentAsync(string image, string server, string version, bool isPrerelease)
    {
        var imageName = $"{server}/{image}:{version}";
        var preReleaseOption = isPrerelease ? " --prerelease" : string.Empty;
        try
        {
            await _processService.RunAsync(
                _containerCli,
                $"image inspect {server}/{image}:{version}",
                output: false
            );
        }

        catch (Exception)
        {
            throw new Exception($"Unable to locate {imageName} image locally. Please run `gh actions-importer update{preReleaseOption}` to fetch the latest image prior to running this command.");
        }
    }

    public async Task<string?> GetLatestImageDigestAsync(string image, string server)
    {
        if (IsWslc)
        {
            return null;
        }

        var (standardOutput, _, _) = await _processService.RunAndCaptureAsync(_containerCli, $"manifest inspect {server}/{image}");
        Manifest? manifest = JsonSerializer.Deserialize<Manifest>(standardOutput);

        return manifest?.GetDigest();
    }

    public async Task<string?> GetCurrentImageDigestAsync(string image, string server)
    {
        if (IsWslc)
        {
            var (standardOutput, _, _) = await _processService.RunAndCaptureAsync(_containerCli, $"image inspect {server}/{image}");
            return GetDigestFromImageInspect(standardOutput);
        }

        var (standardOutput, _, _) = await _processService.RunAndCaptureAsync(_containerCli, $"image inspect --format={{{{.Id}}}} {server}/{image}");

        return standardOutput.Split(":").ElementAtOrDefault(1)?.Trim();
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

    private async Task DockerPullAsync(string image, string server, string version)
    {
        Console.WriteLine($"Updating {server}/{image}:{version}...");
        var (_, standardError, exitCode) = await _processService.RunAndCaptureAsync(
            _containerCli,
            IsWslc ? $"pull {server}/{image}:{version}" : $"pull {server}/{image}:{version} --quiet",
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

    private bool ShouldUseHostNetwork(bool noHostNetwork)
    {
        return !noHostNetwork && !IsWslc;
    }

    private string GetVolumePath(string path)
    {
        return IsWslc ? path.Replace('\\', '/') : path;
    }

    private static string? GetDigestFromImageInspect(string standardOutput)
    {
        using var document = JsonDocument.Parse(standardOutput);

        var root = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().FirstOrDefault()
            : document.RootElement;

        if (root.ValueKind == JsonValueKind.Undefined || root.ValueKind == JsonValueKind.Null)
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

        if (TryGetPropertyIgnoreCase(root, "Id", out var id) && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString()?.Split(':').ElementAtOrDefault(1)?.Trim();
        }

        return null;
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
