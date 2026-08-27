// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Options;

/// <summary>
/// 编译器选项默认值测试
/// </summary>
/// <remarks>
/// 其中三项会直接改变编译产物：语言版本决定语法可用性，
/// <c>GenerateDebugInfo</c> 决定是否产出符号流，<c>TreatWarningsAsErrors</c> 决定一条警告会不会让脚本整体编译失败。
/// </remarks>
public class CompilerOptionsTests
{
    /// <summary>
    /// 默认使用最新语言版本并产出可移植符号
    /// </summary>
    [Fact]
    public void Default_UsesLatestLanguageVersionAndPortablePdb()
    {
        var options = new CompilerOptions();

        Assert.Equal(LanguageVersion.Latest, options.LanguageVersion);
        Assert.Equal(DebugInformationFormat.PortablePdb, options.DebugInformationFormat);
        Assert.True(options.GenerateDebugInfo);
    }

    /// <summary>
    /// 默认警告等级为 4 且不把警告升级为错误
    /// </summary>
    [Fact]
    public void Default_KeepsWarningsAsWarnings()
    {
        var options = new CompilerOptions();

        Assert.Equal(4, options.WarningLevel);
        Assert.False(options.TreatWarningsAsErrors);
    }

    /// <summary>
    /// 默认的四组诊断名单都是可写的空集合
    /// </summary>
    [Fact]
    public void Default_HasEmptyDiagnosticLists()
    {
        var options = new CompilerOptions();

        Assert.Empty(options.PreprocessorSymbols);
        Assert.Empty(options.WarningsAsErrors);
        Assert.Empty(options.WarningsNotAsErrors);
        Assert.Empty(options.DisabledWarnings);

        options.DisabledWarnings.Add("CS8600");

        Assert.Equal("CS8600", Assert.Single(options.DisabledWarnings));
    }

    /// <summary>
    /// 每个实例持有独立的名单集合
    /// </summary>
    [Fact]
    public void Instances_DoNotShareCollections()
    {
        var first = new CompilerOptions();
        var second = new CompilerOptions();

        first.PreprocessorSymbols.Add("DEBUG");

        Assert.NotSame(first.PreprocessorSymbols, second.PreprocessorSymbols);
        Assert.Empty(second.PreprocessorSymbols);
    }
}
