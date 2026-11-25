// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 绑定目录里文件的大小上限
/// </summary>
/// <remarks>
/// RFC-0016「资源绑定与配置节」第 9 条（ADR-0292）：一个文件最多 <see cref="BindingResolver.MaxFileBytes"/> 字节，一个绑定目录里读的文件合计最多 <see cref="BindingResolver.MaxBindingBytes"/> 字节，按原始字节计；超过即解析失败，装配时启动失败。每个用例用自己的临时目录作绑定根
/// </remarks>
public sealed class BindingResolverLimitsTests : IDisposable
{
    /// <summary>
    /// 本用例的 Service Binding 根目录
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-binding-limits-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Limits_AreNotSmallerThanWhatIsDeliveredToPlugins()
    {
        // 下发给插件的一个绑定的属性最多 262144 字节（ADR-0285）：下发得了的，目录里也读得进来
        Assert.Equal(524288, BindingResolver.MaxFileBytes);
        Assert.Equal(1048576, BindingResolver.MaxBindingBytes);
        Assert.True(BindingResolver.MaxFileBytes >= 262144 && BindingResolver.MaxBindingBytes >= 262144);
    }

    [Fact]
    public void Files_AtTheLimits_AreRead()
    {
        // 一个文件正好 512 KiB；两个这样的文件合计正好 1 MiB，type 也在里面
        Write("cert", "ca.crt", new string('c', BindingResolver.MaxFileBytes));
        Write("pair", "type", "blob");
        Write("pair", "tls.crt", new string('a', BindingResolver.MaxFileBytes - 4));
        Write("pair", "tls.key", new string('b', BindingResolver.MaxFileBytes));

        var bindings = BindingResolver.Resolve(Hosts.Configuration(), _root);

        Assert.Equal(BindingResolver.MaxFileBytes, bindings["cert"].Properties["ca.crt"].Length);
        Assert.Equal(("blob", BindingResolver.MaxFileBytes - 4, BindingResolver.MaxFileBytes), (bindings["pair"].Type, bindings["pair"].Properties["tls.crt"].Length, bindings["pair"].Properties["tls.key"].Length));
    }

    [Fact]
    public void Limits_CountTheRawBytes_BeforeTrimming_AndInUtf8()
    {
        // 尾部的换行也占额度：内容去掉首尾空白之后才是属性值，但读的是文件的原始字节
        Write("trailing", "blob", new string('v', BindingResolver.MaxFileBytes) + "\n");
        // 三字节的字符按字节计：按 UTF-16 码元数没有超
        Write("wide", "blob", new string('值', (BindingResolver.MaxFileBytes / 3) + 1));

        Assert.Equal(KernelErrors.BindingTooLarge, Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root, ["trailing"])).Code);
        Assert.Equal(KernelErrors.BindingTooLarge, Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root, ["wide"])).Code);
    }

    [Fact]
    public void AFileOverTheLimit_FailsTheResolution_NamingTheBindingTheFileAndTheLimit_ButNotTheContent()
    {
        Write("db", "uri", "postgres://db/app");
        Write("db", "password", "s3cret" + new string('x', BindingResolver.MaxFileBytes));

        var exception = Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root));

        Assert.Equal(KernelErrors.BindingTooLarge, exception.Code);
        Assert.Equal("File 'password' of binding 'db' is larger than 524288 bytes.", exception.Message);
        Assert.DoesNotContain("s3cret", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("postgres", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_root, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilesOverTheBindingLimitTogether_FailTheResolution_AsTheBindingTotal()
    {
        // 三个文件各自都在单个文件的上限之内，加起来超过一个绑定的上限：报合计，不报单个文件
        Write("bundle", "a", new string('a', BindingResolver.MaxFileBytes));
        Write("bundle", "b", new string('b', BindingResolver.MaxFileBytes - 1));
        Write("bundle", "c", "cc");

        var exception = Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root));

        Assert.Equal(KernelErrors.BindingTooLarge, exception.Code);
        Assert.StartsWith("The files of binding 'bundle' are larger than 1048576 bytes in total; the limit was reached while reading '", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AVeryLargeFile_IsRejectedWithoutReadingItAll()
    {
        // 文件系统报的长度远超上限（稀疏文件）：读到额度再多一个字节就停，以前整个读进内存
        Directory.CreateDirectory(Path.Combine(_root, "huge"));
        using (var stream = new FileStream(Path.Combine(_root, "huge", "blob"), FileMode.CreateNew))
        {
            stream.SetLength(64L * 1024 * 1024);
        }

        var exception = Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root));

        Assert.Equal("File 'blob' of binding 'huge' is larger than 524288 bytes.", exception.Message);
    }

    [Fact]
    public void FilesStartingWithADot_AreNotReadNorCounted()
    {
        // 挂载工具的元数据（..data 之类）不是属性，不读也就不占额度
        Write("db", "..metadata", new string('m', BindingResolver.MaxBindingBytes + 1));
        Write("db", "uri", "postgres://db/app");

        Assert.Equal("postgres://db/app", BindingResolver.Resolve(Hosts.Configuration(), _root)["db"].Value);
    }

    [Fact]
    public void ConfigurationBindings_AreNotLimited()
    {
        // 配置的值已经由配置系统读进内存：上限只管目录来源
        var value = new string('v', BindingResolver.MaxBindingBytes + 1);

        var binding = BindingResolver.Resolve(Hosts.Configuration(("XiHan:Bindings:db:blob", value)), _root)["db"];

        Assert.Equal(value, binding.Properties["blob"]);
    }

    [Fact]
    public void NamedBindings_AreReadInTheGivenOrder_SoTheFirstNamedOneThatIsTooLargeIsReported()
    {
        Write("first", "blob", new string('a', BindingResolver.MaxFileBytes + 1));
        Write("second", "blob", new string('b', BindingResolver.MaxFileBytes + 1));

        // 与目录枚举的先后无关：点名的顺序在前的先读
        Assert.Contains("'second'", Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root, ["second", "first"])).Message, StringComparison.Ordinal);
        Assert.Contains("'first'", Assert.ThrowsAny<FrameworkException>(() => BindingResolver.Resolve(Hosts.Configuration(), _root, ["first", "second"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithAFileOverTheLimit_FailsAtStartup()
    {
        // 装配时解析全部绑定：超限是配置错误，应用起不来，不是装配之后才在某个特性里失败
        Write("db", "uri", new string('u', BindingResolver.MaxFileBytes + 1));

        var exception = Assert.ThrowsAny<FrameworkException>(() => Hosts.BuildApp(b => b.UseServiceBindingRoot(_root)));

        Assert.Equal(KernelErrors.BindingTooLarge, exception.Code);
    }

    [Fact]
    public void Contents_AreDecodedAsBefore()
    {
        // 与以前的 File.ReadAllText 相同：UTF-8 的字节顺序标记不进内容，首尾空白去掉
        File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(Path.Combine(_root, "db")).FullName, "username"), [0xEF, 0xBB, 0xBF, .. "app\n"u8]);

        Assert.Equal("app", BindingResolver.Resolve(Hosts.Configuration(), _root)["db"].Properties["username"]);
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
