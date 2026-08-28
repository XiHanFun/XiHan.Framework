// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using XiHan.Framework.Script.Core;

namespace XiHan.Framework.Script.Tests.Core;

/// <summary>
/// 编译结果契约测试
/// </summary>
/// <remarks>
/// 编译结果既被引擎缓存复用，也被 <c>CompileOrThrowAsync</c>/<c>ValidateSyntaxAsync</c> 消费，
/// 关键点在于成功结果必须带得出程序集字节，失败结果必须只把错误级诊断汇总成消息、同时完整保留诊断集合。
/// </remarks>
public class CompilationResultTests
{
    /// <summary>
    /// 成功结果携带程序集字节与符号，且不产生错误信息
    /// </summary>
    [Fact]
    public void Success_KeepsAssemblySymbolsAndName()
    {
        var assembly = new byte[] { 1, 2, 3 };
        var symbols = new byte[] { 4, 5 };

        var result = CompilationResult.Success(assembly, symbols, "MyAssembly");

        Assert.True(result.IsSuccess);
        Assert.Same(assembly, result.Assembly);
        Assert.Same(symbols, result.Symbols);
        Assert.Equal("MyAssembly", result.AssemblyName);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(0, result.CompilationTimeMs);
        Assert.True(result.Diagnostics.IsDefaultOrEmpty);
    }

    /// <summary>
    /// 只传程序集字节时符号与名称保持为空
    /// </summary>
    [Fact]
    public void Success_WithoutOptionalParts_KeepsThemNull()
    {
        var result = CompilationResult.Success([9]);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Symbols);
        Assert.Null(result.AssemblyName);
    }

    /// <summary>
    /// 失败结果只把错误级诊断汇总进错误信息，但保留全部诊断
    /// </summary>
    [Fact]
    public void Failure_OnlyAggregatesErrorSeverityIntoMessage()
    {
        var error = CreateDiagnostic("XH1001", "编译错误", DiagnosticSeverity.Error);
        var warning = CreateDiagnostic("XH1002", "编译警告", DiagnosticSeverity.Warning);

        var result = CompilationResult.Failure(ImmutableArray.Create(error, warning));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Assembly);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("XH1001", result.ErrorMessage!);
        Assert.DoesNotContain("XH1002", result.ErrorMessage!);
        Assert.Equal(2, result.Diagnostics.Length);
    }

    /// <summary>
    /// 无诊断时失败结果的错误信息为空串
    /// </summary>
    [Fact]
    public void Failure_WithoutDiagnostics_UsesEmptyErrorMessage()
    {
        var result = CompilationResult.Failure([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(string.Empty, result.ErrorMessage);
        Assert.True(result.Diagnostics.IsEmpty);
    }

    /// <summary>
    /// 构造一条诊断信息
    /// </summary>
    /// <param name="id">诊断编号</param>
    /// <param name="message">诊断消息</param>
    /// <param name="severity">严重级别</param>
    /// <returns>诊断信息</returns>
    private static Diagnostic CreateDiagnostic(string id, string message, DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor(id, message, message, "XiHanTests", severity, true);
        return Diagnostic.Create(descriptor, Location.None);
    }
}
