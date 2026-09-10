using System;
using System.Collections.Immutable;
using ActionsImporter.Interfaces;
using ActionsImporter.Services;
using Moq;
using NUnit.Framework;

namespace ActionsImporter.UnitTests;

[TestFixture]
public class ContainerConfigurationTests
{
    [SetUp]
    public void BeforeEachTest()
    {
        Environment.SetEnvironmentVariable("CONTAINER_CLI", null);
    }

    [TearDown]
    public void AfterEachTest()
    {
        Environment.SetEnvironmentVariable("CONTAINER_CLI", null);
    }

    [Test]
    public void CreateDockerService_UsesContainerCliFromEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("CONTAINER_CLI", "wslc");
        var processService = new Mock<IProcessService>();
        var runtimeService = new Mock<IRuntimeService>();

        // Act
        var dockerService = ContainerConfiguration.CreateDockerService(
            processService.Object,
            runtimeService.Object,
            ImmutableDictionary<string, string>.Empty);

        // Assert
        Assert.That(dockerService, Is.TypeOf<WslcDockerService>());
    }

    [Test]
    public void GetContainerCli_EnvironmentOverridesConfigurationVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("CONTAINER_CLI", "podman");
        var configurationVariables = ImmutableDictionary<string, string>.Empty.Add("CONTAINER_CLI", "wslc");

        // Act
        var containerCli = ContainerConfiguration.GetContainerCli(configurationVariables);

        // Assert
        Assert.That(containerCli, Is.EqualTo("podman"));
    }

    [Test]
    public void GetContainerCli_TrimsConfiguredValue()
    {
        // Arrange
        var configurationVariables = ImmutableDictionary<string, string>.Empty.Add("CONTAINER_CLI", " wslc ");

        // Act
        var containerCli = ContainerConfiguration.GetContainerCli(configurationVariables);

        // Assert
        Assert.That(containerCli, Is.EqualTo("wslc"));
    }
}
