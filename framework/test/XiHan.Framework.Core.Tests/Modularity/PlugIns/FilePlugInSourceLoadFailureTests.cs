// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity.PlugIns;

/// <summary>
/// 文件插件源加载失败包装测试
/// </summary>
/// <remarks>
/// 一次插件解析包含「加载程序集」与「扫描模块类型」两步，对上层是一件事。
/// 因此任何一步失败都必须统一以 <see cref="XiHanException"/> 上抛，并在消息里点名出问题的插件文件，
/// 否则调用方只能拿到一个不知来源的 IO / 映像异常，无从判断是哪个插件坏了。
/// </remarks>
public class FilePlugInSourceLoadFailureTests : IDisposable
{
    private readonly string _folder;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public FilePlugInSourceLoadFailureTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// 插件文件不存在时包装为曦寒异常并带上文件路径
    /// </summary>
    [Fact]
    public void GetModules_WhenFileMissing_ThrowsXiHanExceptionWithFilePath()
    {
        var missing = Path.Combine(_folder, "absent.dll");
        var source = new FilePlugInSource(missing);

        var exception = Assert.Throws<XiHanException>(() => source.GetModules());

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
        // 原始加载异常必须保留在内层，诊断信息不能丢
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// 传入相对路径时包装为曦寒异常并带上文件路径
    /// </summary>
    /// <remarks>加载上下文只接受绝对路径，相对路径原本会抛出无上下文的 ArgumentException。</remarks>
    [Fact]
    public void GetModules_WhenPathIsRelative_ThrowsXiHanExceptionWithFilePath()
    {
        var source = new FilePlugInSource("relative-plugin.dll");

        var exception = Assert.Throws<XiHanException>(() => source.GetModules());

        Assert.Contains("relative-plugin.dll", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// 文件不是托管映像时包装为曦寒异常并带上文件路径
    /// </summary>
    [Fact]
    public void GetModules_WhenFileIsNotManagedImage_ThrowsXiHanExceptionWithFilePath()
    {
        var fake = Path.Combine(_folder, "not-an-assembly.dll");
        File.WriteAllText(fake, "这不是一个托管程序集");
        var source = new FilePlugInSource(fake);

        var exception = Assert.Throws<XiHanException>(() => source.GetModules());

        Assert.Contains(fake, exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// 多个插件路径时异常消息点名真正出问题的那一个
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetModules_WhenOnePathAmongManyFails_NamesTheFailingFile()
    {
        var location = typeof(XiHanModule).Assembly.Location;
        Assert.SkipUnless(!string.IsNullOrEmpty(location), "无法定位框架核心程序集文件，跳过混合路径验证。");

        var missing = Path.Combine(_folder, "broken.dll");
        var source = new FilePlugInSource(location, missing);

        var exception = Assert.Throws<XiHanException>(() => source.GetModules());

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(location, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 加载成功的路径不受包装影响
    /// </summary>
    /// <remarks>反例：包装只针对失败路径，正常扫描必须照旧返回模块类型而不是被误伤成异常。</remarks>
    [Fact(Timeout = 60_000)]
    public void GetModules_WhenPathIsLoadable_DoesNotThrow()
    {
        var location = typeof(XiHanModule).Assembly.Location;
        Assert.SkipUnless(!string.IsNullOrEmpty(location), "无法定位框架核心程序集文件，跳过正常路径验证。");

        var modules = new FilePlugInSource(location).GetModules();

        Assert.All(modules, type => Assert.True(XiHanModuleHelper.IsXiHanModule(type)));
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }
        catch (IOException)
        {
            // 临时目录清理失败不影响断言结论
        }
        catch (UnauthorizedAccessException)
        {
            // 临时目录清理失败不影响断言结论
        }

        GC.SuppressFinalize(this);
    }
}
