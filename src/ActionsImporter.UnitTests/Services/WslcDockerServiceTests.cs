using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ActionsImporter.Interfaces;
using ActionsImporter.Services;
using Moq;
using NUnit.Framework;

namespace ActionsImporter.UnitTests.Services;

[TestFixture]
public class WslcDockerServiceTests
{
#pragma warning disable CS8618
    private WslcDockerService _dockerService;
    private Mock<IProcessService> _processService;
    private Mock<IRuntimeService> _runtimeService;
#pragma warning restore CS8618

    [SetUp]
    public void BeforeEachTest()
    {
        _processService = new Mock<IProcessService>();
        _runtimeService = new Mock<IRuntimeService>();
        _dockerService = new WslcDockerService(_processService.Object, _runtimeService.Object);
    }

    [TearDown]
    public void AfterEachTest()
    {
        Environment.SetEnvironmentVariable("DOCKER_ARGS", null);
        Environment.SetEnvironmentVariable("WSLC_ARGS", null);
        Environment.SetEnvironmentVariable("CONTAINER_ARGS", null);
    }

    [Test]
    public async Task ExecuteCommandAsync_InvokesWslc_WithBackendSpecificArguments_ReturnsTrue()
    {
        var image = "actions-importer/cli";
        var server = "ghcr.io";
        var version = "latest";
        var arguments = new[] { "run", "this", "command" };
        var currentDirectory = Directory.GetCurrentDirectory();
        var volumePath = currentDirectory.Replace('\\', '/');

        Environment.SetEnvironmentVariable("WSLC_ARGS", "--detach");

        _processService.Setup(handler =>
            handler.RunAsync(
                "wslc",
                $"run --rm -t --detach -v \"{volumePath}\":/data {server}/{image}:{version} {string.Join(' ', arguments)}",
                currentDirectory,
                new[] { new ValueTuple<string, string>("MSYS_NO_PATHCONV", "1") },
                true
            )
        ).Returns(Task.CompletedTask);

        await _dockerService.ExecuteCommandAsync(image, server, version, false, arguments);

        _processService.VerifyAll();
    }

    [Test]
    public void VerifyDockerRunningAsync_WslcInstalled_NoException()
    {
        _processService.Setup(handler =>
            handler.RunAsync(
                "wslc",
                "version",
                It.IsAny<string?>(),
                It.IsAny<IEnumerable<(string, string)>?>(),
                It.IsAny<bool>()
            )
        ).Returns(Task.CompletedTask);

        Assert.DoesNotThrowAsync(() => _dockerService.VerifyDockerRunningAsync());
    }

    [Test]
    public async Task UpdateImageAsync_Wslc_PullsWithoutQuietFlag()
    {
        var image = "actions-importer/cli";
        var server = "ghcr.io";
        var version = "latest";

        _processService.Setup(handler =>
            handler.RunAndCaptureAsync(
                "wslc",
                $"pull {server}/{image}:{version}",
                It.IsAny<string?>(),
                It.IsAny<IEnumerable<(string, string)>?>(),
                It.IsAny<bool>(),
                null
            )
        ).ReturnsAsync(("", "", 0));

        await _dockerService.UpdateImageAsync(image, server, version);

        _processService.VerifyAll();
    }

    [Test]
    public async Task GetCurrentImageDigest_Wslc_ParsesDigestFromImageInspect()
    {
        var image = "actions-importer/cli:latest";
        var server = "ghcr.io";
        var inspectResult = @"
[
  {
    ""RepoDigests"": [
      ""ghcr.io/actions-importer/cli@sha256:67eed1493c461efd993be9777598a456562f4e0c6b0bddcb19d819220a06dd4b""
    ]
  }
]";

        _processService.Setup(handler =>
            handler.RunAndCaptureAsync(
                "wslc",
                $"image inspect {server}/{image}",
                It.IsAny<string?>(),
                It.IsAny<IEnumerable<(string, string)>?>(),
                It.IsAny<bool>(),
                null
            )
        ).ReturnsAsync((inspectResult, "", 0));

        var result = await _dockerService.GetCurrentImageDigestAsync(image, server);

        Assert.AreEqual("67eed1493c461efd993be9777598a456562f4e0c6b0bddcb19d819220a06dd4b", result);
        _processService.VerifyAll();
    }

    [Test]
    public async Task GetLatestImageDigest_Wslc_ReturnsNull()
    {
        var result = await _dockerService.GetLatestImageDigestAsync("actions-importer/cli:latest", "ghcr.io");

        Assert.IsNull(result);
        _processService.VerifyNoOtherCalls();
    }
}
