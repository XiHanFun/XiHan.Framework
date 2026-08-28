// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using XiHan.Framework.Script.Core;

namespace XiHan.Framework.Script.Tests.Core;

/// <summary>
/// 脚本执行结果契约测试
/// </summary>
/// <remarks>
/// <see cref="ScriptResult"/> 是引擎、扩展方法、监控器三方共用的数据契约：
/// 引擎按它写入耗时与缓存信息，扩展方法按 <c>IsSuccess</c>/<c>ErrorMessage</c> 决定是否抛异常，
/// 监控器按它落日志。因此三个工厂方法的字段填充与泛型结果的转换语义必须锁死。
/// </remarks>
public class ScriptResultTests
{
    /// <summary>
    /// 成功结果携带返回值且不带错误信息
    /// </summary>
    [Fact]
    public void Success_WithValue_MarksSuccessAndKeepsValue()
    {
        var result = ScriptResult.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, (int)result.Value!);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
        Assert.False(result.FromCache);
        Assert.Null(result.CacheKey);
        Assert.Equal(0, result.ExecutionTimeMs);
        Assert.Equal(0, result.CompilationTimeMs);
        Assert.Null(result.MemoryUsage);
    }

    /// <summary>
    /// 不传返回值时成功结果的返回值为空
    /// </summary>
    [Fact]
    public void Success_WithoutValue_KeepsValueNull()
    {
        var result = ScriptResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    /// <summary>
    /// 失败结果保留错误信息与原始异常
    /// </summary>
    [Fact]
    public void Failure_WithException_KeepsMessageAndException()
    {
        var inner = new InvalidOperationException("底层异常");

        var result = ScriptResult.Failure("执行失败", inner);

        Assert.False(result.IsSuccess);
        Assert.Equal("执行失败", result.ErrorMessage);
        Assert.Same(inner, result.Exception);
        Assert.Null(result.Value);
    }

    /// <summary>
    /// 编译失败结果只把错误级诊断汇总进错误信息，但完整保留全部诊断
    /// </summary>
    [Fact]
    public void CompilationFailure_OnlyAggregatesErrorSeverityIntoMessage()
    {
        var error = CreateDiagnostic("XH0001", "编译错误", DiagnosticSeverity.Error);
        var warning = CreateDiagnostic("XH0002", "编译警告", DiagnosticSeverity.Warning);
        var diagnostics = ImmutableArray.Create(error, warning);

        var result = ScriptResult.CompilationFailure(diagnostics);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("XH0001", result.ErrorMessage!);
        Assert.DoesNotContain("XH0002", result.ErrorMessage!);
        Assert.Equal(2, result.Diagnostics.Length);
    }

    /// <summary>
    /// 没有诊断信息时编译失败结果的错误信息为空串而不是 null
    /// </summary>
    [Fact]
    public void CompilationFailure_WithoutDiagnostics_UsesEmptyErrorMessage()
    {
        var result = ScriptResult.CompilationFailure(ImmutableArray<Diagnostic>.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    /// <summary>
    /// 附加数据默认是可写的空字典
    /// </summary>
    [Fact]
    public void Metadata_ByDefault_IsEmptyAndWritable()
    {
        var result = new ScriptResult();

        Assert.Empty(result.Metadata);

        result.Metadata["k"] = "v";

        Assert.Equal("v", result.Metadata["k"]);
    }

    /// <summary>
    /// 字符串表示按成败切换前缀
    /// </summary>
    [Fact]
    public void ToString_SwitchesPrefixBySuccessFlag()
    {
        Assert.Equal("Success: 7", ScriptResult.Success(7).ToString());
        Assert.Equal("Failure: 出错了", ScriptResult.Failure("出错了").ToString());
    }

    /// <summary>
    /// 泛型结果在类型匹配时返回强类型值
    /// </summary>
    [Fact]
    public void GenericValue_WhenTypeMatches_ReturnsTypedValue()
    {
        var result = new ScriptResult<int>
        {
            IsSuccess = true,
            Value = 42
        };

        Assert.Equal(42, result.Value);
        Assert.Equal(42, (int)((ScriptResult)result).Value!);
    }

    /// <summary>
    /// 泛型结果在类型不匹配时返回默认值而不是抛异常
    /// </summary>
    [Fact]
    public void GenericValue_WhenTypeMismatches_ReturnsDefault()
    {
        var result = new ScriptResult<int>();
        ((ScriptResult)result).Value = "不是整数";

        Assert.Equal(0, result.Value);
    }

    /// <summary>
    /// 泛型失败结果保留错误信息与异常
    /// </summary>
    [Fact]
    public void GenericFailure_KeepsMessageAndException()
    {
        var inner = new InvalidOperationException("底层异常");

        var result = ScriptResult<string>.Failure("执行失败", inner);

        Assert.False(result.IsSuccess);
        Assert.Equal("执行失败", result.ErrorMessage);
        Assert.Same(inner, result.Exception);
        Assert.Null(result.Value);
    }

    /// <summary>
    /// 从基类结果转换时逐字段搬运耗时、缓存与诊断信息
    /// </summary>
    [Fact]
    public void FromBase_CopiesEveryContractField()
    {
        var exception = new InvalidOperationException("底层异常");
        var diagnostics = ImmutableArray.Create(CreateDiagnostic("XH0003", "诊断", DiagnosticSeverity.Warning));
        var memory = new MemoryUsage { MemoryBefore = 10, MemoryAfter = 30 };
        var source = new ScriptResult
        {
            IsSuccess = true,
            Value = "曦寒",
            ErrorMessage = "无",
            Exception = exception,
            Diagnostics = diagnostics,
            ExecutionTimeMs = 11,
            CompilationTimeMs = 22,
            MemoryUsage = memory,
            FromCache = true,
            CacheKey = "cache-key"
        };
        source.Metadata["k"] = "v";

        var converted = ScriptResult<string>.FromBase(source);

        Assert.True(converted.IsSuccess);
        Assert.Equal("曦寒", converted.Value);
        Assert.Equal("无", converted.ErrorMessage);
        Assert.Same(exception, converted.Exception);
        Assert.Single(converted.Diagnostics);
        Assert.Equal(11, converted.ExecutionTimeMs);
        Assert.Equal(22, converted.CompilationTimeMs);
        Assert.Same(memory, converted.MemoryUsage);
        Assert.True(converted.FromCache);
        Assert.Equal("cache-key", converted.CacheKey);
        Assert.Equal("v", converted.Metadata["k"]);
    }

    /// <summary>
    /// 从基类结果转换时类型不匹配退化为默认值，不抛异常
    /// </summary>
    [Fact]
    public void FromBase_WhenValueTypeMismatches_FallsBackToDefault()
    {
        var source = ScriptResult.Success("不是整数");

        var converted = ScriptResult<int>.FromBase(source);

        Assert.True(converted.IsSuccess);
        Assert.Equal(0, converted.Value);
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
