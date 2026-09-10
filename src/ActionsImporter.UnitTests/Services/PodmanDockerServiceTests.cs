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
public class PodmanDockerServiceTests
{
#pragma warning disable CS8618
    private PodmanDockerService _dockerService;
    private Mock<IProcessService> _processService;
    private Mock<IRuntimeService> _runtimeService;
#pragma warning restore CS8618

    [SetUp]
    public void BeforeEachTest()
    {
        _processService = new Mock<IProcessService>();
        _runtimeService = new Mock<IRuntimeService>();
        _dockerService = new PodmanDockerService(_processService.Object, _runtimeService.Object);
    }

    [TearDown]
    public void AfterEachTest()
    {
        Environment.SetEnvironmentVariable("DOCKER_ARGS", null);
        Environment.SetEnvironmentVariable("PODMAN_ARGS", null);
        Environment.SetEnvironmentVariable("CONTAINER_ARGS", null);
    }

    [Test]
    public async Task ExecuteCommandAsync_InvokesPodman_WithBackendSpecificArguments_ReturnsTrue()
    {
        var image = "actions-importer/cli";
        var server = "ghcr.io";
        var version = "latest";
        var arguments = new[] { "run", "this", "command" };

        Environment.SetEnvironmentVariable("PODMAN_ARGS", "--detach");

        _processService.Setup(handler =>
            handler.RunAsync(
                "podman",
                $"run --rm -t --network=host --detach -v \"{Directory.GetCurrentDirectory()}\":/data {server}/{image}:{version} {string.Join(' ', arguments)}",
                Directory.GetCurrentDirectory(),
                new[] { new ValueTuple<string, string>("MSYS_NO_PATHCONV", "1") },
                true
            )
        ).Returns(Task.CompletedTask);

        await _dockerService.ExecuteCommandAsync(image, server, version, false, arguments);

        _processService.VerifyAll();
    }

    [Test]
    public void VerifyDockerRunningAsync_PodmanInstalled_NoException()
    {
        _processService.Setup(handler =>
            handler.RunAsync(
                "podman",
                "version",
                It.IsAny<string?>(),
                It.IsAny<IEnumerable<(string, string)>?>(),
                It.IsAny<bool>()
            )
        ).Returns(Task.CompletedTask);

        Assert.DoesNotThrowAsync(() => _dockerService.VerifyDockerRunningAsync());
    }

    [Test]
    public async Task UpdateImageAsync_Podman_PullsWithQuietFlag()
    {
        var image = "actions-importer/cli";
        var server = "ghcr.io";
        var version = "latest";

        _processService.Setup(handler =>
            handler.RunAndCaptureAsync(
                "podman",
                $"pull {server}/{image}:{version} --quiet",
                It.IsAny<string?>(),
                It.IsAny<IEnumerable<(string, string)>?>(),
                It.IsAny<bool>(),
                null
            )
        ).ReturnsAsync(("", "", 0));

        await _dockerService.UpdateImageAsync(image, server, version);

        _processService.VerifyAll();
    }
}
