using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// A host environment that reports itself as Production, so the startup checks run as they would on a server.
/// </summary>
public class ProductionHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "Tests";

    public string ContentRootPath { get; set; } = ".";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
