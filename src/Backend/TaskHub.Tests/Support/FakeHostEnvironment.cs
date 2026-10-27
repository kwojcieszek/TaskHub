using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace TaskHub.Tests.Support;

/// <summary>
/// The little a service needs to know about where the application runs: its content root.
/// </summary>
public sealed class FakeHostEnvironment : IHostEnvironment
{
    public FakeHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
    }

    public string EnvironmentName { get; set; } = "Testing";

    public string ApplicationName { get; set; } = "TaskHub.Tests";

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
