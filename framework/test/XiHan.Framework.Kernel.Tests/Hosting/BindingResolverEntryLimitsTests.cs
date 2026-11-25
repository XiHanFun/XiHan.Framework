// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 绑定目录的条目数上限
/// </summary>
/// <remarks>
/// RFC-0016「资源绑定与配置节」第 10 条（ADR-0301）：解析全部绑定时根目录下最多 <see cref="BindingResolver.MaxBindings"/> 个绑定，一个绑定目录里读的文件最多 <see cref="BindingResolver.MaxBindingFiles"/> 个（以 <c>.</c> 开头的不计）；先数、后读，超过即解析失败，装配时启动失败。每个用例用自己的临时目录作绑定根
/// </remarks>
public sealed class BindingResolverEntryLimitsTests : IDisposable
{
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
    public void Limits_HoldTheTypeFileAndAsManyPropertiesAsCanBeDeliveredToPlugins()
    {
        // 下发给插件的一个绑定最多 64 个属性（ADR-0285）：type 文件之外 64 个属性都放得下，文件数取它的两倍
        Assert.Equal(128, BindingResolver.MaxBindingFiles);
        Assert.Equal(128, BindingResolver.MaxBindings);
        Assert.True(BindingResolver.MaxBindingFiles >= 64 + 1);
    }

    [Fact]
    public void ABinding_WithExactlyTheMaximumNumberOfFiles_IsRead()
    {
        // 正好 128 个：type、uri 与 126 个别的属性；以 . 开头的不计
        Write("db", "type", "connection:relational");
        Write("db", "uri", "postgres://db/app");
        for (var i = 0; i < BindingResolver.MaxBindingFiles - 2; i++)
        {
            Write("db", $"p{i}", "v");
        }

        Write("db", "..data", "metadata");

        var binding = BindingResolver.Resolve(Hosts.Configuration(), _root)["db"];

        Assert.Equal("connection:relational", binding.Type);
        Assert.Equal(BindingResolver.MaxBindingFiles - 1, binding.Properties.Count);
    }

    [Fact]
    public void ABinding_WithOneFileMore_FailsTheResolution_BeforeAnyOfItsFilesIsRead()
    {
        // 先数、后读：多出的一个文件让解析在读任何内容之前失败，超过大小上限的那个文件也就报不出来
        for (var i = 0; i < BindingResolver.MaxBindingFiles; i++)
        {
            Write("db", $"p{i}", "v");
        }

        Write("db", "s3cret", new string('x', BindingResolver.MaxFileBytes + 1));

        var exception = Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root));

        Assert.Equal(KernelErrors.BindingTooLarge, exception.Code);
        Assert.Equal("Binding 'db' has more than 128 files.", exception.Message);
        Assert.DoesNotContain(_root, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilesStartingWithADot_AreNotCounted()
    {
        for (var i = 0; i < BindingResolver.MaxBindingFiles; i++)
        {
            Write("db", $"p{i}", "v");
        }

        for (var i = 0; i < 8; i++)
        {
            Write("db", $"..meta{i}", "m");
        }

        Assert.Equal(BindingResolver.MaxBindingFiles, BindingResolver.Resolve(Hosts.Configuration(), _root)["db"].Properties.Count);
    }

    [Fact]
    public void TheRoot_WithExactlyTheMaximumNumberOfBindings_IsRead()
    {
        for (var i = 0; i < BindingResolver.MaxBindings; i++)
        {
            Write($"b{i}", "uri", $"redis://b{i}");
        }

        Assert.Equal(BindingResolver.MaxBindings, BindingResolver.Resolve(Hosts.Configuration(), _root).Count);
    }

    [Fact]
    public void TheRoot_WithOneBindingMore_FailsTheResolution_BeforeAnyBindingIsRead()
    {
        // 每个子目录都是一个绑定、都计入（空目录也算）；多出一个时一个文件也不读，超过大小上限的那个文件报不出来
        for (var i = 0; i < BindingResolver.MaxBindings; i++)
        {
            Directory.CreateDirectory(Path.Combine(_root, $"b{i}"));
        }

        Write("huge", "blob", new string('x', BindingResolver.MaxFileBytes + 1));

        var exception = Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root));

        Assert.Equal(KernelErrors.BindingTooLarge, exception.Code);
        Assert.Equal("The service binding root contains more than 128 bindings.", exception.Message);
        Assert.DoesNotContain(_root, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedResolution_ReadsOnlyTheNamedBindings_SoOnlyTheirFilesAreCounted()
    {
        // 插件宿主只读清单声明的绑定：根目录下的绑定数不核对，点了名的绑定照样有文件数的上限
        for (var i = 0; i <= BindingResolver.MaxBindings; i++)
        {
            Directory.CreateDirectory(Path.Combine(_root, $"b{i}"));
        }

        Write("db", "uri", "postgres://db/app");
        for (var i = 0; i <= BindingResolver.MaxBindingFiles; i++)
        {
            Write("wide", $"p{i}", "v");
        }

        Assert.Equal("postgres://db/app", BindingResolver.Resolve(Hosts.Configuration(), _root, ["db"])["db"].Value);
        Assert.Equal("Binding 'wide' has more than 128 files.", Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root, ["db", "wide"])).Message);
    }

    [Fact]
    public void ConfigurationBindings_AreNotCounted()
    {
        // 配置来源不受条目数的上限约束：值已经由配置系统读进内存
        var values = Enumerable.Range(0, BindingResolver.MaxBindings + 1).Select(static i => ($"XiHan:Bindings:b{i}", "x")).ToArray();

        Assert.Equal(BindingResolver.MaxBindings + 1, BindingResolver.Resolve(Hosts.Configuration(values), _root).Count);
    }

    [Fact]
    public void Build_WithTooManyBindingsOrFiles_FailsAtStartup()
    {
        for (var i = 0; i <= BindingResolver.MaxBindingFiles; i++)
        {
            Write("db", $"p{i}", "v");
        }

        Assert.Equal(KernelErrors.BindingTooLarge, Assert.ThrowsAny<FrameworkException>(() => Hosts.BuildApp(b => b.UseServiceBindingRoot(_root))).Code);

        for (var i = 0; i < BindingResolver.MaxBindings; i++)
        {
            Directory.CreateDirectory(Path.Combine(_root, $"b{i}"));
        }

        Assert.Equal("The service binding root contains more than 128 bindings.", Assert.ThrowsAny<FrameworkException>(() => Hosts.BuildApp(b => b.UseServiceBindingRoot(_root))).Message);
    }

    /// <summary>
    /// 在绑定根下的一个绑定目录里写一个文件
    /// </summary>
    private void Write(string binding, string file, string content)
    {
        var directory = Path.Combine(_root, binding);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, file), content);
    }
}
