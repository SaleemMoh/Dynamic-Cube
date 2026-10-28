using System.Security.Cryptography;
using System.Text;
using DynamicCube.Platform.Files;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Platform.Tests;

public sealed class FileSystemFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dc-files-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemFileStorage _storage;

    public FileSystemFileStorageTests()
    {
        _storage = new FileSystemFileStorage(Options.Create(new FileStorageOptions { RootPath = _root }), TimeProvider.System);
    }

    [Fact]
    public async Task Saves_reads_and_deletes_a_file()
    {
        var bytes = Encoding.UTF8.GetBytes("شهادة تصنيف");
        var ct = TestContext.Current.CancellationToken;

        var stored = await _storage.SaveAsync(new MemoryStream(bytes), "Certificate.PDF", "application/pdf", ct);

        stored.Key.ShouldEndWith(".pdf");
        stored.Length.ShouldBe(bytes.Length);
        stored.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        (await _storage.ExistsAsync(stored.Key, ct)).ShouldBeTrue();

        await using (var read = await _storage.OpenReadAsync(stored.Key, ct))
        using (var copy = new MemoryStream())
        {
            await read.CopyToAsync(copy, ct);
            copy.ToArray().ShouldBe(bytes);
        }

        await _storage.DeleteAsync(stored.Key, ct);
        (await _storage.ExistsAsync(stored.Key, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Unsafe_extensions_are_dropped()
    {
        var stored = await _storage.SaveAsync(new MemoryStream([1]), "evil.p/../hp", "application/octet-stream", TestContext.Current.CancellationToken);

        Path.GetExtension(stored.Key).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("2026/10/../../../secret")]
    [InlineData("/etc/passwd")]
    [InlineData("2026/10/not-a-guid.pdf")]
    public async Task Rejects_keys_outside_the_generated_pattern(string key) =>
        await Should.ThrowAsync<ArgumentException>(() => _storage.ExistsAsync(key, TestContext.Current.CancellationToken));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
