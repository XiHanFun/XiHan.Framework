// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.VirtualFileSystem.Utilities;

namespace XiHan.Framework.VirtualFileSystem.Tests.Utilities;

/// <summary>
/// 虚拟路径解析器测试
/// </summary>
/// <remarks>
/// 所有对外 API（GetFile / GetDirectoryContents / EnumerateFiles）都先经过这里，
/// 路径形态一旦漂移会整片失效，因此按输入形态穷举：相对/绝对、正反斜杠、尾部斜杠、
/// ~/、embedded://、memory://、以及 ../ 穿越型输入。
/// 注意：本类型只做「文本规范化」，不负责裁掉 ../ 段，越权访问的实际防线在物理提供器上，
/// 相关断言见 VirtualPhysicalFileProviderTests 与 VirtualFileSystemTests。
/// </remarks>
public class PathResolverTests
{
    /// <summary>
    /// 常规路径统一规范成以斜杠开头、无尾部斜杠的形式
    /// </summary>
    [Theory]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    [InlineData("/", "/")]
    [InlineData("config/app.json", "/config/app.json")]
    [InlineData("/config/app.json", "/config/app.json")]
    [InlineData("\\config\\app.json", "/config/app.json")]
    [InlineData("/config/app.json/", "/config/app.json")]
    [InlineData("  /config/app.json  ", "/config/app.json")]
    [InlineData("~/config/app.json", "/config/app.json")]
    public void ResolveVirtualPath_ForCommonForms_ReturnsRootedPath(string input, string expected)
    {
        Assert.Equal(expected, PathResolver.ResolveVirtualPath(input));
    }

    /// <summary>
    /// 带协议前缀的路径会剥掉协议与程序集段
    /// </summary>
    [Theory]
    [InlineData("embedded://MyAssembly/config/app.json", "/config/app.json")]
    [InlineData("EMBEDDED://MyAssembly/config/app.json", "/config/app.json")]
    [InlineData("embedded://MyAssembly", "/")]
    [InlineData("memory://cache/a.txt", "/cache/a.txt")]
    [InlineData("mem://cache/a.txt", "/cache/a.txt")]
    [InlineData("MEMORY://cache/a.txt", "/cache/a.txt")]
    public void ResolveVirtualPath_ForSchemePrefixes_StripsScheme(string input, string expected)
    {
        Assert.Equal(expected, PathResolver.ResolveVirtualPath(input));
    }

    /// <summary>
    /// 传入 null 抛参数空异常
    /// </summary>
    [Fact]
    public void ResolveVirtualPath_WhenNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _ = PathResolver.ResolveVirtualPath(null!));
    }

    /// <summary>
    /// 含 ../ 的敌意输入仍然被归一为斜杠分隔、以 / 开头的虚拟路径
    /// </summary>
    /// <remarks>
    /// 这里锁的是「规范化不会因为敌意输入而退化」：结果必须仍是虚拟路径形态（以 / 开头、无反斜杠），
    /// 从而保证下游提供器拿到的是可判定的统一形态。是否允许越界由提供器决定，不在本类型职责内。
    /// </remarks>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\win.ini")]
    [InlineData("/a/../../b")]
    [InlineData("~/../secret.txt")]
    public void ResolveVirtualPath_ForTraversalInput_StillReturnsRootedSlashPath(string input)
    {
        var resolved = PathResolver.ResolveVirtualPath(input);

        Assert.StartsWith("/", resolved);
        Assert.DoesNotContain("\\", resolved);
    }

    /// <summary>
    /// 默认不保留尾部斜杠
    /// </summary>
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    [InlineData("a", "/a")]
    [InlineData("/a/b/", "/a/b")]
    [InlineData("\\a\\b", "/a/b")]
    [InlineData("  /a/b  ", "/a/b")]
    [InlineData("/", "/")]
    public void NormalizeVirtualPath_ByDefault_TrimsTrailingSlash(string? input, string expected)
    {
        Assert.Equal(expected, PathResolver.NormalizeVirtualPath(input!));
    }

    /// <summary>
    /// 显式要求时保留尾部斜杠
    /// </summary>
    [Theory]
    [InlineData("/a/b/", "/a/b/")]
    [InlineData("/a/b", "/a/b")]
    [InlineData("/", "/")]
    [InlineData("a/", "/a/")]
    public void NormalizeVirtualPath_WhenKeepTrailingSlash_PreservesIt(string input, string expected)
    {
        Assert.Equal(expected, PathResolver.NormalizeVirtualPath(input, keepTrailingSlash: true));
    }

    /// <summary>
    /// 合并路径时左右两侧的多余斜杠会被折叠成单个分隔符
    /// </summary>
    [Theory]
    [InlineData("/a", "b", "/a/b")]
    [InlineData("/a/", "/b", "/a/b")]
    [InlineData("/", "b", "/b")]
    [InlineData("/a", "b\\c", "/a/b/c")]
    [InlineData("/a", "", "/a")]
    [InlineData("/a", null, "/a")]
    [InlineData(null, "b", "/b")]
    [InlineData(null, null, "/")]
    public void CombineVirtualPath_ForMixedInputs_JoinsWithSingleSlash(string? left, string? right, string expected)
    {
        Assert.Equal(expected, PathResolver.CombineVirtualPath(left!, right!));
    }

    /// <summary>
    /// 判断路径归属时按段比较，不做前缀误判
    /// </summary>
    [Theory]
    [InlineData("/a/b", "/a", true)]
    [InlineData("/a", "/a", true)]
    [InlineData("/A/B", "/a", true)]
    [InlineData("/a/b/c", "/a/b", true)]
    [InlineData("/ab", "/a", false)]
    [InlineData("/b", "/a", false)]
    [InlineData("/a/b", "/", true)]
    [InlineData("/a/b", null, true)]
    public void IsPathUnder_ComparesBySegment(string path, string? rootPath, bool expected)
    {
        Assert.Equal(expected, PathResolver.IsPathUnder(path, rootPath!));
    }

    /// <summary>
    /// 嵌入式路径解析会丢掉协议与程序集名，只保留资源内路径
    /// </summary>
    [Theory]
    [InlineData("embedded://MyAssembly/a/b.txt", "/a/b.txt")]
    [InlineData("embedded://MyAssembly\\a\\b.txt", "/a/b.txt")]
    [InlineData("MyAssembly/a/b.txt", "/a/b.txt")]
    [InlineData("embedded://MyAssembly", "/")]
    [InlineData("MyAssembly", "/")]
    public void ResolveEmbeddedPath_StripsSchemeAndAssemblySegment(string input, string expected)
    {
        Assert.Equal(expected, PathResolver.ResolveEmbeddedPath(input));
    }

    /// <summary>
    /// 嵌入式路径传入 null 抛参数空异常
    /// </summary>
    [Fact]
    public void ResolveEmbeddedPath_WhenNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _ = PathResolver.ResolveEmbeddedPath(null!));
    }

    /// <summary>
    /// 内存路径解析同时支持 memory:// 与 mem:// 两种前缀
    /// </summary>
    [Theory]
    [InlineData("memory://cache/a.txt", "/cache/a.txt")]
    [InlineData("mem://cache/a.txt", "/cache/a.txt")]
    [InlineData("MEM://cache/a.txt", "/cache/a.txt")]
    [InlineData("memory:///cache/a.txt", "/cache/a.txt")]
    [InlineData("cache/a.txt", "/cache/a.txt")]
    [InlineData("memory://", "/")]
    public void ResolveMemoryPath_SupportsBothPrefixes(string input, string expected)
    {
        Assert.Equal(expected, PathResolver.ResolveMemoryPath(input));
    }

    /// <summary>
    /// 内存路径传入 null 抛参数空异常
    /// </summary>
    [Fact]
    public void ResolveMemoryPath_WhenNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _ = PathResolver.ResolveMemoryPath(null!));
    }
}
