using Microsoft.Extensions.Options;
using TaskHub.Application.Options;
using TaskHub.Infrastructure.Storage;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Storage;

public class FileSystemAttachmentStorageTests : IDisposable
{
    private const string Key = "9f86d081884c7d659a2feae0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "taskhub-unit-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAttachmentStorage _storage;

    public FileSystemAttachmentStorageTests()
    {
        Directory.CreateDirectory(_contentRoot);

        var options = Options.Create(new AttachmentStorageOptions { FileSystem = new FileSystemStorageOptions { RootPath = "attachments" } });
        _storage = new FileSystemAttachmentStorage(options, new FakeHostEnvironment(_contentRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SavedContent_CanBeReadBackAndDeleted()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var before = await _storage.ExistsAsync(Key);
        await _storage.SaveAsync(Key, new MemoryStream(content));
        var after = await _storage.ExistsAsync(Key);

        byte[] read;

        await using (var stream = await _storage.OpenReadAsync(Key))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            read = buffer.ToArray();
        }

        await _storage.DeleteAsync(Key);

        Assert.False(before);
        Assert.True(after);
        Assert.Equal(content, read);
        Assert.False(await _storage.ExistsAsync(Key));
    }

    [Fact]
    public async Task Content_IsStoredUnderTheRootInDirectoriesNamedAfterTheHash()
    {
        await _storage.SaveAsync(Key, new MemoryStream(new byte[] { 1 }));

        var expected = Path.Combine(_contentRoot, "attachments", Key[..2], Key.Substring(2, 2), Key);
        var files = Directory.GetFiles(_contentRoot, "*", SearchOption.AllDirectories);

        Assert.True(File.Exists(expected));
        Assert.Equal(expected, Assert.Single(files));
    }

    [Fact]
    public async Task UppercaseKey_NamesTheSameContent()
    {
        await _storage.SaveAsync(Key.ToUpperInvariant(), new MemoryStream(new byte[] { 1 }));

        Assert.True(await _storage.ExistsAsync(Key));
    }

    [Fact]
    public async Task SavingAgain_ReplacesTheContent_AndLeavesNoTemporaryFile()
    {
        await _storage.SaveAsync(Key, new MemoryStream(new byte[] { 1 }));
        await _storage.SaveAsync(Key, new MemoryStream(new byte[] { 2, 3 }));

        await using var stream = await _storage.OpenReadAsync(Key);

        Assert.Equal(2, stream.Length);
        Assert.Single(Directory.GetFiles(_contentRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task MissingContent_IsReportedOnRead_AndIgnoredOnDelete()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.OpenReadAsync(Key));

        await _storage.DeleteAsync(Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config")]
    [InlineData("zz86d081884c7d659a2feae0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")]
    [InlineData("9f86d081884c7d659a2feae0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08ff")]
    [InlineData("9f/6d081884c7d659a2feae0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")]
    public async Task KeyThatIsNotAHash_IsRefusedBeforeAnyPathIsBuilt(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.ExistsAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(key, new MemoryStream(new byte[] { 1 })));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.OpenReadAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync(key));

        Assert.Empty(Directory.GetFiles(_contentRoot, "*", SearchOption.AllDirectories));
    }
}
