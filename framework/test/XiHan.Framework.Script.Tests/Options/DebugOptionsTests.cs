// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Debugging;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Options;

/// <summary>
/// 脚本调试选项测试
/// </summary>
/// <remarks>
/// 三个预设(默认/详细/生产)之间的差异就是这个类型的全部价值，
/// 逐项锁死可以防止"生产预设悄悄打开调试"这类回归。
/// </remarks>
public class DebugOptionsTests
{
    /// <summary>
    /// 默认预设不开调试与性能分析，但保留代码映射
    /// </summary>
    [Fact]
    public void Default_KeepsDebuggingOffButMappingOn()
    {
        var options = DebugOptions.Default;

        Assert.False(options.EnableDebugging);
        Assert.True(options.GenerateFullDebugInfo);
        Assert.True(options.PreserveCodeMapping);
        Assert.True(options.EnableVariableWatch);
        Assert.False(options.EnableProfiling);
        Assert.Equal(DebugLevel.Information, options.DebugLevel);
        Assert.Equal(10000, options.MaxDebugOutputLength);
        Assert.Equal(300000, options.DebugSessionTimeoutMs);
        Assert.Empty(options.Breakpoints);
    }

    /// <summary>
    /// 默认预设每次返回新实例，断点集合互不共享
    /// </summary>
    [Fact]
    public void Default_ReturnsIndependentInstances()
    {
        var first = DebugOptions.Default;
        var second = DebugOptions.Default;

        Assert.NotSame(first, second);

        first.Breakpoints.Add(new Breakpoint { LineNumber = 1 });

        Assert.Empty(second.Breakpoints);
    }

    /// <summary>
    /// 详细预设打开调试、变量监视与性能分析
    /// </summary>
    [Fact]
    public void Verbose_TurnsOnEveryDiagnosticSwitch()
    {
        var options = DebugOptions.Verbose();

        Assert.True(options.EnableDebugging);
        Assert.True(options.GenerateFullDebugInfo);
        Assert.True(options.PreserveCodeMapping);
        Assert.True(options.EnableVariableWatch);
        Assert.True(options.EnableProfiling);
        Assert.Equal(DebugLevel.Verbose, options.DebugLevel);
    }

    /// <summary>
    /// 生产预设关闭调试相关开关并只保留错误级输出
    /// </summary>
    [Fact]
    public void Production_TurnsOffEveryDiagnosticSwitch()
    {
        var options = DebugOptions.Production();

        Assert.False(options.EnableDebugging);
        Assert.False(options.GenerateFullDebugInfo);
        Assert.False(options.PreserveCodeMapping);
        Assert.False(options.EnableVariableWatch);
        Assert.False(options.EnableProfiling);
        Assert.Equal(DebugLevel.Error, options.DebugLevel);
    }

    /// <summary>
    /// 预设不改动输出长度与会话超时，保持与默认一致
    /// </summary>
    [Fact]
    public void Presets_KeepOutputLimitsAtDefault()
    {
        var verbose = DebugOptions.Verbose();
        var production = DebugOptions.Production();

        Assert.Equal(10000, verbose.MaxDebugOutputLength);
        Assert.Equal(10000, production.MaxDebugOutputLength);
        Assert.Equal(300000, verbose.DebugSessionTimeoutMs);
        Assert.Equal(300000, production.DebugSessionTimeoutMs);
    }
}
