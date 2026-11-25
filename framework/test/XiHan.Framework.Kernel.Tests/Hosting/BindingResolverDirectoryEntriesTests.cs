// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 绑定根目录与绑定目录里哪些目录项算数
/// </summary>
/// <remarks>
/// RFC-0016「资源绑定与配置节」第 11 条（ADR-0311）：根目录下以 <c>.</c> 开头的子目录不是绑定，不读、不计入 <see cref="BindingResolver.MaxBindings"/>，解析全部绑定与只解析点了名的绑定都如此；目录项可以是符号链接，名字取目录项自己的，内容取链接最终指向的。
/// 符号链接的用例按 Kubernetes 的两种挂法搭目录：projected volume 直接挂到根目录，Secret 卷挂到一个绑定目录。每个用例用自己的临时目录作绑定根
/// </remarks>
public sealed class BindingResolverDirectoryEntriesTests : IDisposable
{
    /// <summary>
    /// Kubernetes 原子写入时带时间戳的目录名
    /// </summary>
    private const string Stamp = "..2026_11_25_09_30_00.123456789";

    /// <summary>
    /// 挂到根目录的 projected volume 里的绑定名，按序数排好
    /// </summary>
    private static readonly string[] _projectedNames = ["cache", "db"];

    /// <summary>
    /// 本用例的 Service Binding 根目录
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-binding-entries-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void DirectoriesStartingWithADot_AreNotBindings()
    {
        Write("db", "uri", "postgres://db/app");
        Write("..data", "uri", "postgres://stale/app");
        Write(Stamp, "uri", "postgres://stale/app");
        Write(".hidden", "type", "queue");

        var bindings = BindingResolver.Resolve(Hosts.Configuration(), _root);

        Assert.Equal("db", Assert.Single(bindings).Key);
        Assert.Equal("postgres://db/app", bindings["db"].Value);
    }

    [Fact]
    public void DirectoriesStartingWithADot_AreNotCounted()
    {
        // 128 个绑定正好在上限上，旁边以 . 开头的目录不占名额
        for (var i = 0; i < BindingResolver.MaxBindings; i++)
        {
            Write($"b{i}", "uri", $"redis://b{i}");
        }

        Directory.CreateDirectory(Path.Combine(_root, "..data"));
        Directory.CreateDirectory(Path.Combine(_root, Stamp));
        Directory.CreateDirectory(Path.Combine(_root, ".hidden"));

        Assert.Equal(BindingResolver.MaxBindings, BindingResolver.Resolve(Hosts.Configuration(), _root).Count);
    }

    [Fact]
    public void DirectoriesStartingWithADot_AreNotRead()
    {
        // 不读：里面超过大小上限的文件与多于文件数上限的文件都不让解析失败
        Write("db", "uri", "postgres://db/app");
        Write("..data", "blob", new string('x', BindingResolver.MaxFileBytes + 1));
        for (var i = 0; i <= BindingResolver.MaxBindingFiles; i++)
        {
            Write(".wide", $"p{i}", "v");
        }

        Assert.Equal("db", Assert.Single(BindingResolver.Resolve(Hosts.Configuration(), _root)).Key);
    }

    [Fact]
    public void NamedResolution_SkipsDirectoriesStartingWithADot_Too()
    {
        // 清单里的绑定名本来不以 . 开头；点名的读法与不限名字时同一条规则，点到了也不读
        Write("db", "uri", "postgres://db/app");
        Write("..data", "uri", "postgres://stale/app");

        Assert.Equal("db", Assert.Single(BindingResolver.Resolve(Hosts.Configuration(), _root, ["..data", "db"])).Key);
    }

    [Fact]
    public void AProjectedVolumeMountedAtTheRoot_IsReadThroughTheLinksOfItsBindings()
    {
        // projected volume 直接挂到根目录：..<时间> 目录放内容，..data 指向它，每个绑定名指向 ..data/<名>
        Write(Path.Combine(Stamp, "db"), "type", "connection:relational\n");
        Write(Path.Combine(Stamp, "db"), "uri", "postgres://db/app\n");
        Write(Path.Combine(Stamp, "cache"), "uri", "cache:6379\n");
        LinkDirectory("..data", Stamp);
        LinkDirectory("db", Path.Combine("..data", "db"));
        LinkDirectory("cache", Path.Combine("..data", "cache"));

        var bindings = BindingResolver.Resolve(Hosts.Configuration(), _root);

        // 名字取链接自己的名字，内容取链接最终指向的；..data 与带时间戳的目录以 . 开头，不是绑定
        Assert.Equal(_projectedNames, bindings.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(("connection:relational", "postgres://db/app"), (bindings["db"].Type, bindings["db"].Value));
        Assert.Equal("cache:6379", bindings["cache"].Value);
    }

    [Fact]
    public void AProjectedVolume_IsReadAsItIsAfterKubernetesSwapsItsDataLink()
    {
        // 卷更新时 Kubernetes 写一个新的带时间戳的目录，再把 ..data 换过去：每次解析读的是那一刻的内容
        Write(Path.Combine(Stamp, "db"), "uri", "postgres://old/app");
        LinkDirectory("..data", Stamp);
        LinkDirectory("db", Path.Combine("..data", "db"));
        Assert.Equal("postgres://old/app", BindingResolver.Resolve(Hosts.Configuration(), _root)["db"].Value);

        const string next = "..2026_11_25_09_35_00.987654321";
        Write(Path.Combine(next, "db"), "uri", "postgres://new/app");
        Directory.Delete(Path.Combine(_root, "..data"));
        LinkDirectory("..data", next);

        var bindings = BindingResolver.Resolve(Hosts.Configuration(), _root);

        Assert.Equal("db", Assert.Single(bindings).Key);
        Assert.Equal("postgres://new/app", bindings["db"].Value);
    }

    [Fact]
    public void ASecretVolumeMountedAtABindingDirectory_IsReadThroughTheLinksOfItsKeys()
    {
        // Secret 卷挂到一个绑定目录：目录里的 ..data 与带时间戳的目录是子目录、不是属性，每个键是指向 ..data/<键> 的符号链接
        var db = Path.Combine(_root, "db");
        Write(Path.Combine("db", Stamp), "type", "connection:relational\n");
        Write(Path.Combine("db", Stamp), "uri", "postgres://db/app\n");
        Write(Path.Combine("db", Stamp), "password", "s3cret\n");
        LinkDirectory(Path.Combine("db", "..data"), Stamp);
        foreach (var key in new[] { "type", "uri", "password" })
        {
            LinkFile(Path.Combine(db, key), Path.Combine("..data", key));
        }

        var binding = BindingResolver.Resolve(Hosts.Configuration(), _root)["db"];

        Assert.Equal(("connection:relational", "postgres://db/app"), (binding.Type, binding.Value));
        Assert.Equal(new Dictionary<string, string> { ["uri"] = "postgres://db/app", ["password"] = "s3cret" }, binding.Properties);
    }

    [Fact]
    public void ALinkedFile_IsLimitedByWhatItPointsTo()
    {
        // 大小按链接指向的内容计：链接本身只有几个字节，指向的文件超过上限照样解析失败
        Write(Path.Combine("db", Stamp), "blob", new string('x', BindingResolver.MaxFileBytes + 1));
        LinkDirectory(Path.Combine("db", "..data"), Stamp);
        LinkFile(Path.Combine(_root, "db", "blob"), Path.Combine("..data", "blob"));

        Assert.Equal(KernelErrors.BindingTooLarge, Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root)).Code);
    }

    /// <summary>
    /// 在绑定根下的一个目录里写一个文件
    /// </summary>
    private void Write(string directory, string file, string content)
    {
        var path = Path.Combine(_root, directory);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, file), content);
    }

    /// <summary>
    /// 在绑定根下建一个指向目录的符号链接，目标按相对路径写（与 Kubernetes 相同）
    /// </summary>
    private void LinkDirectory(string link, string target)
        => CreateLink(() => Directory.CreateSymbolicLink(Path.Combine(_root, link), target));

    /// <summary>
    /// 建一个指向文件的符号链接，目标按相对路径写
    /// </summary>
    private static void LinkFile(string link, string target)
        => CreateLink(() => File.CreateSymbolicLink(link, target));

    /// <summary>
    /// 建符号链接，拿不到权限时跳过这条用例
    /// </summary>
    /// <remarks>
    /// Windows 上建符号链接要 SeCreateSymbolicLinkPrivilege（开发人员模式或提升的进程）；其他系统不要求权限
    /// </remarks>
    private static void CreateLink(Action create)
    {
        try
        {
            create();
        }
        catch (Exception exception) when (OperatingSystem.IsWindows() && exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Creating symbolic links on Windows needs Developer Mode or an elevated process; the test runs where links can be created, such as Linux.");
        }
    }
}
