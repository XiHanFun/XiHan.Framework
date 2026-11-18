// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 临时文件与对象的文件名互不混淆（ADR-0212）
/// </summary>
/// <remarks>
/// 临时文件的名字片段是键的编码不会产出的字符，判定只看文件名：键里、目录里、根路径里出现同样的文字都不算临时文件
/// </remarks>
public sealed class FileSystemTemporaryFileTests : IDisposable
{
    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task KeysContainingTheTemporaryText_AreListed_AndSurviveARestart()
    {
        var container = new ContainerName("docs");
        var first = await CreateAsync(_root.Path);
        Assert.True((await first.PutAsync(container, new BlobKey("report.xihan-tmp-1"), Bytes("a"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await first.PutAsync(container, new BlobKey("draft.xihan-tmp-x/notes"), Bytes("bb"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(["draft.xihan-tmp-x/notes", "report.xihan-tmp-1"], await KeysAsync(first, container));

        // 重启扫描不把它们当作上次崩溃留下的临时文件删掉
        var second = await CreateAsync(_root.Path);
        Assert.Equal(3, second.UsedBytes);
        Assert.Equal(["draft.xihan-tmp-x/notes", "report.xihan-tmp-1"], await KeysAsync(second, container));
        Assert.Equal("a", Encoding.UTF8.GetString((await second.ReadAllBytesAsync(container, new BlobKey("report.xihan-tmp-1"), CancellationToken.None)).ValueOrThrow()));
    }

    [Fact]
    public async Task ARootPathContainingTheTemporaryText_KeepsItsObjects()
    {
        var root = Path.Combine(_root.Path, "store.xihan-tmp-root");
        var container = new ContainerName("docs");
        var first = await CreateAsync(root);
        Assert.True((await first.PutAsync(container, new BlobKey("a.txt"), Bytes("abc"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(["a.txt"], await KeysAsync(first, container));

        var second = await CreateAsync(root);
        Assert.Equal(3, second.UsedBytes);
        Assert.Equal(["a.txt"], await KeysAsync(second, container));
    }

    [Fact]
    public async Task Initialize_CleansCurrentAndLegacyTemporaryFiles_ButKeepsObjectsWithMetadata()
    {
        var container = new ContainerName("ctr");
        var first = await CreateAsync(_root.Path);
        // 键恰好是旧版临时文件的样子：有元数据文件，是对象
        var lookalike = "y.xihan-tmp-" + new string('a', 32);
        Assert.True((await first.PutAsync(container, new BlobKey("x/" + lookalike), Bytes("12345"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);

        var data = Path.Combine(_root.Path, "ctr", "data", "x");
        var current = Path.Combine(data, "z" + FileSystemBlobStore.TemporaryMarker + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(data, "z.xihan-tmp-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllBytesAsync(current, new byte[100]);
        await File.WriteAllBytesAsync(legacy, new byte[100]);

        var second = await CreateAsync(_root.Path);
        Assert.Equal(5, second.UsedBytes);
        Assert.False(File.Exists(current));
        Assert.False(File.Exists(legacy));
        Assert.Equal(["x/" + lookalike], await KeysAsync(second, container));
    }

    [Fact]
    public async Task Initialize_CleansCurrentAndLegacyTemporaryMetadataFiles_ButKeepsTheMetadataOfObjects()
    {
        var container = new ContainerName("ctr");
        var text = new BlobPutOptions { ContentType = "text/plain" };
        var first = await CreateAsync(_root.Path);
        // 键 x/y 的旧版元数据临时文件名恰好是另一个对象的内容文件名；那个对象自己的元数据文件仍以 .json 结尾
        var suffix = Guid.NewGuid().ToString("N");
        var lookalike = "x/y.json.xihan-tmp-" + suffix;
        Assert.True((await first.PutAsync(container, new BlobKey("x/y"), Bytes("12345"), text, CancellationToken.None)).IsSuccess);
        Assert.True((await first.PutAsync(container, new BlobKey(lookalike), Bytes("67"), text, CancellationToken.None)).IsSuccess);

        // 进程在写好元数据临时文件之后、换上之前崩溃留下的（ADR-0239）
        var meta = Path.Combine(_root.Path, "ctr", "meta", "x");
        var current = Path.Combine(meta, "y.json" + FileSystemBlobStore.TemporaryMarker + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(meta, "y.json.xihan-tmp-" + suffix);
        await File.WriteAllBytesAsync(current, new byte[100]);
        await File.WriteAllBytesAsync(legacy, new byte[100]);

        var second = await CreateAsync(_root.Path);
        Assert.False(File.Exists(current));
        Assert.False(File.Exists(legacy));
        Assert.True(File.Exists(Path.Combine(meta, "y.json")));
        Assert.True(File.Exists(Path.Combine(meta, "y.json.xihan-tmp-" + suffix + ".json")));
        Assert.Equal(7, second.UsedBytes);
        Assert.Equal(["x/y", lookalike], await KeysAsync(second, container));
        // 内容类型是写入时给的，不是元数据缺失时按文件信息补出的缺省值
        foreach (var key in new[] { "x/y", lookalike })
        {
            Assert.Equal("text/plain", (await second.HeadAsync(container, new BlobKey(key), CancellationToken.None)).ValueOrThrow().ContentType);
        }
    }

    /// <summary>
    /// 在给定根目录上建出并初始化存储
    /// </summary>
    private static async Task<FileSystemBlobStore> CreateAsync(string root)
        => await FileSystemBlobStore.CreateAsync(new FileSystemObjectStorageOptions { RootPath = root }, TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);

    /// <summary>
    /// 列出容器里的全部键
    /// </summary>
    private static async Task<List<string>> KeysAsync(FileSystemBlobStore store, ContainerName container)
        => [.. (await store.ListAsync(container, BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items.Select(static m => m.Key.Value)];

    /// <summary>
    /// UTF-8 字节
    /// </summary>
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
