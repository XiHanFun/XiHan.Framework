// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.IO;

namespace XiHan.Framework.Utils.Tests.IO;

/// <summary>
/// 路径帮助类测试。
/// </summary>
/// <remarks>
/// 两条契约：一是目录穿越判定必须卡在分隔符边界上，裸前缀匹配会放行同级的兄弟目录；
/// 二是「产出数据」的方法（净化文件名、算路径摘要）不能随运行平台变形——
/// 走 <c>Path.GetInvalidFileNameChars()</c> 之类的平台相关 API，同一个输入在
/// Windows（41 个非法字符）与 Linux（只有 \0 与 /）上会得到不同结果。
/// </remarks>
public class PathHelperTests
{
    private static readonly string BaseDirectory = Path.Combine(Path.GetTempPath(), "xihan-path-base");

    [Fact]
    public void IsPathSafe_拒绝跳出基准目录()
    {
        Assert.False(PathHelper.IsPathSafe("../outside.txt", BaseDirectory));
        Assert.False(PathHelper.IsPathSafe("../../etc/passwd", BaseDirectory));
        Assert.False(PathHelper.IsPathSafe("sub/../../outside.txt", BaseDirectory));
    }

    [Fact]
    public void IsPathSafe_拒绝同级的同前缀兄弟目录()
    {
        // 裸前缀匹配的经典绕过：xihan-path-baseXXX 以 xihan-path-base 开头，
        // 但它是基准目录的兄弟，不在基准目录之内
        Assert.False(PathHelper.IsPathSafe("../xihan-path-baseXXX/secret.txt", BaseDirectory));
        Assert.False(PathHelper.IsPathSafe("../xihan-path-base-backup/secret.txt", BaseDirectory));
    }

    [Fact]
    public void IsPathSafe_放行基准目录之内()
    {
        Assert.True(PathHelper.IsPathSafe("file.txt", BaseDirectory));
        Assert.True(PathHelper.IsPathSafe("sub/file.txt", BaseDirectory));
        Assert.True(PathHelper.IsPathSafe("sub/deeper/file.txt", BaseDirectory));
        // 基准目录自身算在内
        Assert.True(PathHelper.IsPathSafe(".", BaseDirectory));
    }

    [Fact]
    public void IsPathSafe_空参数一律拒绝()
    {
        Assert.False(PathHelper.IsPathSafe("", BaseDirectory));
        Assert.False(PathHelper.IsPathSafe("file.txt", ""));
        Assert.False(PathHelper.IsPathSafe(" ", " "));
    }

    [Fact]
    public void IsSubPath_同样卡在分隔符边界上()
    {
        Assert.True(PathHelper.IsSubPath(BaseDirectory, Path.Combine(BaseDirectory, "sub", "file.txt")));
        Assert.True(PathHelper.IsSubPath(BaseDirectory, BaseDirectory));
        Assert.False(PathHelper.IsSubPath(BaseDirectory, BaseDirectory + "XXX"));
        Assert.False(PathHelper.IsSubPath(BaseDirectory, Path.Combine(Path.GetTempPath(), "elsewhere", "file.txt")));
    }

    [Theory]
    [InlineData("a<b>c", "a_b_c")]
    [InlineData("a:b", "a_b")]
    [InlineData("a\"b", "a_b")]
    [InlineData("a|b?c*d", "a_b_c_d")]
    [InlineData("a/b", "a_b")]
    [InlineData("a\\b", "a_b")]
    public void SanitizeFileName_按各平台限制的并集净化(string input, string expected)
    {
        // Linux 的 Path.GetInvalidFileNameChars() 只有 \0 与 /，走它会让这些字符原样留下，
        // 净化产物落库、进 URL 后在 Windows 侧再落地时才炸
        Assert.Equal(expected, PathHelper.SanitizeFileName(input));
    }

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("nul", "nul_")]
    [InlineData("COM1.txt", "COM1_.txt")]
    [InlineData("report.", "report")]
    [InlineData("report ", "report")]
    public void SanitizeFileName_保留名与末尾点空格不看运行平台(string input, string expected)
    {
        Assert.Equal(expected, PathHelper.SanitizeFileName(input));
    }

    [Fact]
    public void SanitizeFileName_非法字符被替换而不是丢弃()
    {
        Assert.Equal("___", PathHelper.SanitizeFileName("///"));
        Assert.Equal("---", PathHelper.SanitizeFileName("///", '-'));
    }

    [Fact]
    public void SanitizeFileName_净化后为空时给出兜底名()
    {
        // 全是末尾点/空格，TrimEnd 之后什么都不剩
        Assert.Equal("file", PathHelper.SanitizeFileName("..."));
        Assert.Equal(string.Empty, PathHelper.SanitizeFileName(" "));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("LPT9")]
    [InlineData("aux")]
    [InlineData("a<b")]
    [InlineData("a/b")]
    [InlineData("report.")]
    [InlineData("report ")]
    public void IsValidFileName_判定不看运行平台(string fileName)
    {
        Assert.False(PathHelper.IsValidFileName(fileName));
    }

    [Theory]
    [InlineData("report.txt")]
    [InlineData("CONSOLE.txt")]
    [InlineData("a.b.c")]
    [InlineData(".gitignore")]
    public void IsValidFileName_放行合法名(string fileName)
    {
        Assert.True(PathHelper.IsValidFileName(fileName));
    }

    [Fact]
    public void GetPathHash_分隔符风格不影响结果()
    {
        // 同一个路径写成两种分隔符风格，摘要必须一致，否则拿它当缓存键会重复入缓存
        Assert.Equal(PathHelper.GetPathHash("a/b/c"), PathHelper.GetPathHash(@"a\b\c"));
        Assert.Equal(PathHelper.GetPathHash("a//b///c"), PathHelper.GetPathHash("a/b/c"));
    }

    [Fact]
    public void GetPathHash_不同路径给出不同结果()
    {
        Assert.NotEqual(PathHelper.GetPathHash("a/b"), PathHelper.GetPathHash("a/c"));
        Assert.Equal(string.Empty, PathHelper.GetPathHash(" "));
    }

    [Fact]
    public void PathComparison_与运行平台的文件系统语义一致()
    {
        var expected = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Assert.Equal(expected, PathHelper.PathComparison);
    }
}
