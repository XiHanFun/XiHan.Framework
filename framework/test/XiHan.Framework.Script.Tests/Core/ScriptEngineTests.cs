// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Exceptions;
using XiHan.Framework.Script.Extensions;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Core;

/// <summary>
/// 脚本引擎端到端测试
/// </summary>
/// <remarks>
/// 这里是唯一会真正驱动 Roslyn 编译的一组用例，用例数量刻意压到最小：
/// 只覆盖"求值、语句赋值、编译失败、缓存命中、文件安全闸门"这几条主干路径，
/// 每个等待都套了硬超时，避免编译器或线程池异常时把 CI 挂死。
/// 真实编译的耗时不可预测，因此断言全部落在结果契约上，不锁具体毫秒数。
/// </remarks>
public class ScriptEngineTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// 初始化测试，准备独立的临时目录
    /// </summary>
    public ScriptEngineTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// 空白脚本代码直接返回失败，不进入编译
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCodeBlank_ReturnsFailure(string code)
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.ExecuteAsync(code));

        Assert.False(result.IsSuccess);
        Assert.Equal("脚本代码不能为空", result.ErrorMessage);
    }

    /// <summary>
    /// 语句脚本通过约定的 result 变量回传返回值
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StatementAssigningResult_ReturnsValue()
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.ExecuteAsync("result = 1 + 1;"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, (int)result.Value!);
        Assert.NotNull(result.MemoryUsage);
        Assert.True(result.ExecutionTimeMs >= 0);
    }

    /// <summary>
    /// 强类型执行在类型匹配时给出强类型返回值
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Generic_WhenTypeMatches_ReturnsTypedValue()
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.ExecuteAsync<int>("result = 6 * 7;"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(42, result.Value);
    }

    /// <summary>
    /// 强类型执行在类型不匹配时退化为默认值而不是抛异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Generic_WhenTypeMismatches_ReturnsDefault()
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.ExecuteAsync<string>("result = 6 * 7;"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Null(result.Value);
    }

    /// <summary>
    /// 表达式求值返回表达式本身的值
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ArithmeticExpression_ReturnsValue()
    {
        using var engine = new ScriptEngine();

        var value = await Guard(engine.EvaluateAsync("1 + 1"));

        Assert.NotNull(value);
        Assert.Equal(2, (int)value!);
    }

    /// <summary>
    /// 表达式求值支持字符串结果与强类型出参
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_Generic_StringExpression_ReturnsString()
    {
        using var engine = new ScriptEngine();

        var value = await Guard(engine.EvaluateAsync<string>("\"曦寒\""));

        Assert.Equal("曦寒", value);
    }

    /// <summary>
    /// 表达式非法时求值返回空而不是抛异常
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenExpressionInvalid_ReturnsNull()
    {
        using var engine = new ScriptEngine();

        var value = await Guard(engine.EvaluateAsync("1 +"));

        Assert.Null(value);
    }

    /// <summary>
    /// 语法错误的脚本以编译失败结果返回，并带回错误级诊断
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCodeHasSyntaxError_ReturnsCompilationFailure()
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.ExecuteAsync("result = ;"));

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.False(result.Diagnostics.IsDefaultOrEmpty);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 空白脚本的编译请求返回不带诊断的失败结果
    /// </summary>
    [Fact]
    public async Task CompileAsync_WhenCodeBlank_ReturnsFailureWithoutDiagnostics()
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.CompileAsync("   "));

        Assert.False(result.IsSuccess);
        Assert.True(result.Diagnostics.IsDefaultOrEmpty);
        Assert.Equal(string.Empty, result.ErrorMessage);
        Assert.Null(result.Assembly);
    }

    /// <summary>
    /// 合法脚本编译后产出程序集字节、符号与动态程序集名
    /// </summary>
    [Fact]
    public async Task CompileAsync_ValidCode_ProducesAssemblyBytesAndSymbols()
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.CompileAsync("result = 1;"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Assembly);
        Assert.NotEmpty(result.Assembly!);
        Assert.NotNull(result.AssemblyName);
        Assert.StartsWith("DynamicScript_", result.AssemblyName!);
        // 编译器选项默认生成调试信息，符号流必须一起产出
        Assert.NotNull(result.Symbols);
        Assert.True(result.CompilationTimeMs >= 0);
    }

    /// <summary>
    /// 相同缓存键的第二次执行命中编译缓存
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithSameCacheKey_SecondCallHitsCache()
    {
        using var engine = new ScriptEngine();
        var cacheKey = "cache-" + Guid.NewGuid().ToString("N");

        var first = await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.WithCacheKey(cacheKey)));
        var second = await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.WithCacheKey(cacheKey)));

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.False(first.FromCache);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.True(second.FromCache);
        Assert.Equal(cacheKey, second.CacheKey);

        var statistics = engine.GetStatistics();
        Assert.Equal(2, statistics.TotalExecutions);
        Assert.Equal(2, statistics.SuccessfulExecutions);
        Assert.Equal(0, statistics.FailedExecutions);
        Assert.Equal(1, statistics.CacheHits);
        Assert.Equal(1, statistics.CacheMisses);
        Assert.Equal(1, statistics.CacheSize);
        Assert.Equal(100d, statistics.SuccessRate);
        Assert.Equal(50d, statistics.CacheHitRate);
    }

    /// <summary>
    /// 显式禁用缓存后同一段脚本每次都重新编译
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCacheDisabled_NeverServesFromCache()
    {
        using var engine = new ScriptEngine();
        var cacheKey = "cache-" + Guid.NewGuid().ToString("N");

        await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.DisableCache().WithCacheKey(cacheKey)));
        var second = await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.DisableCache().WithCacheKey(cacheKey)));

        Assert.False(second.FromCache);

        var statistics = engine.GetStatistics();
        Assert.Equal(0, statistics.CacheHits);
        Assert.Equal(2, statistics.CacheMisses);
        Assert.Equal(0, statistics.CacheSize);
    }

    /// <summary>
    /// 清除缓存后缓存尺寸归零且后续执行重新记为未命中
    /// </summary>
    [Fact]
    public async Task ClearCache_ResetsCacheSizeAndForcesRecompile()
    {
        using var engine = new ScriptEngine();
        var cacheKey = "cache-" + Guid.NewGuid().ToString("N");

        await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.WithCacheKey(cacheKey)));
        engine.ClearCache();

        Assert.Equal(0, engine.GetStatistics().CacheSize);

        var again = await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.WithCacheKey(cacheKey)));

        Assert.False(again.FromCache);
        Assert.Equal(2, engine.GetStatistics().CacheMisses);
    }

    /// <summary>
    /// 统计信息返回的是快照，取回后不再随引擎变化
    /// </summary>
    [Fact]
    public async Task GetStatistics_ReturnsSnapshotInsteadOfLiveReference()
    {
        using var engine = new ScriptEngine();

        var before = engine.GetStatistics();
        await Guard(engine.ExecuteAsync("result = 1;"));
        var after = engine.GetStatistics();

        Assert.NotSame(before, after);
        Assert.Equal(0, before.TotalExecutions);
        Assert.Equal(1, after.TotalExecutions);
    }

    /// <summary>
    /// 创建实例在脚本返回匹配类型时给出实例
    /// </summary>
    [Fact]
    public async Task CreateInstanceAsync_WhenScriptReturnsMatchingType_ReturnsInstance()
    {
        using var engine = new ScriptEngine();

        var instance = await Guard(engine.CreateInstanceAsync<string>("result = \"曦寒\";"));

        Assert.Equal("曦寒", instance);
    }

    /// <summary>
    /// 创建实例在脚本执行失败时返回空
    /// </summary>
    [Fact]
    public async Task CreateInstanceAsync_WhenExecutionFails_ReturnsNull()
    {
        using var engine = new ScriptEngine();

        var instance = await Guard(engine.CreateInstanceAsync<string>("result = ;"));

        Assert.Null(instance);
    }

    /// <summary>
    /// 释放引擎会清空缓存且可重复调用
    /// </summary>
    [Fact]
    public async Task Dispose_ClearsCacheAndIsIdempotent()
    {
        var engine = new ScriptEngine();
        await Guard(engine.ExecuteAsync("result = 1;", ScriptOptions.Default.WithCacheKey("cache-" + Guid.NewGuid().ToString("N"))));

        engine.Dispose();
        engine.Dispose();

        Assert.Equal(0, engine.GetStatistics().CacheSize);
    }

    /// <summary>
    /// 空白文件路径直接返回失败
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteFileAsync_WhenPathBlank_ReturnsFailure(string path)
    {
        using var engine = new ScriptEngine();

        var result = await Guard(engine.ExecuteFileAsync(path));

        Assert.False(result.IsSuccess);
        Assert.Equal("脚本文件路径不能为空", result.ErrorMessage);
    }

    /// <summary>
    /// 文件不存在时返回失败并回显路径
    /// </summary>
    [Fact]
    public async Task ExecuteFileAsync_WhenFileMissing_ReturnsFailure()
    {
        using var engine = new ScriptEngine();
        var path = Path.Combine(_tempDirectory, "missing.cs");

        var result = await Guard(engine.ExecuteFileAsync(path));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("脚本文件不存在", result.ErrorMessage!);
        Assert.Contains(path, result.ErrorMessage!);
    }

    /// <summary>
    /// 扩展名不在白名单内的脚本文件被安全闸门拦下
    /// </summary>
    [Fact]
    public async Task ExecuteFileAsync_WhenExtensionNotAllowed_ThrowsSecurityException()
    {
        using var engine = new ScriptEngine();
        var path = Path.Combine(_tempDirectory, "script.exe");
        await File.WriteAllTextAsync(path, "result = 1;", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<ScriptSecurityException>(() => Guard(engine.ExecuteFileAsync(path)));

        Assert.Equal("InvalidFileExtension", exception.ViolationType);
        Assert.Contains(".exe", exception.Message);
    }

    /// <summary>
    /// 超出大小上限的脚本文件被安全闸门拦下
    /// </summary>
    [Fact]
    public async Task ExecuteFileAsync_WhenFileTooLarge_ThrowsSecurityException()
    {
        Assert.SkipUnless(IsPathFriendly(_tempDirectory), $"临时目录路径包含引擎判定为危险的字符(.. ~ $)：{_tempDirectory}，跳过该组验证。");

        using var engine = new ScriptEngine();
        var path = Path.Combine(_tempDirectory, "big.cs");
        await File.WriteAllTextAsync(path, new string('a', 128), TestContext.Current.CancellationToken);
        var options = ScriptOptions.Default.WithSecurity(security => security.MaxFileSize = 16);

        var exception = await Assert.ThrowsAsync<ScriptSecurityException>(() => Guard(engine.ExecuteFileAsync(path, options)));

        Assert.Equal("FileTooLarge", exception.ViolationType);
    }

    /// <summary>
    /// 白名单内的脚本文件正常执行，并以文件路径与写入时间派生缓存键
    /// </summary>
    [Fact]
    public async Task ExecuteFileAsync_WithAllowedExtension_ExecutesFileContent()
    {
        Assert.SkipUnless(IsPathFriendly(_tempDirectory), $"临时目录路径包含引擎判定为危险的字符(.. ~ $)：{_tempDirectory}，跳过该组验证。");

        using var engine = new ScriptEngine();
        var path = Path.Combine(_tempDirectory, "ok.cs");
        await File.WriteAllTextAsync(path, "result = 40 + 2;", TestContext.Current.CancellationToken);

        var result = await Guard(engine.ExecuteFileAsync(path));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(42, (int)result.Value!);
        Assert.NotNull(result.CacheKey);
        Assert.StartsWith("file:", result.CacheKey!);
    }

    /// <summary>
    /// 关闭安全检查后扩展名白名单不再生效
    /// </summary>
    [Fact]
    public async Task ExecuteFileAsync_WhenSecurityDisabled_IgnoresExtensionWhitelist()
    {
        using var engine = new ScriptEngine();
        var path = Path.Combine(_tempDirectory, "script.script");
        await File.WriteAllTextAsync(path, "result = 7;", TestContext.Current.CancellationToken);

        var result = await Guard(engine.ExecuteFileAsync(path, ScriptOptions.Default.DisableSecurity()));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(7, (int)result.Value!);
    }

    /// <summary>
    /// 脚本声明受限命名空间时被程序集安全检查拦下
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenScriptDeclaresForbiddenNamespace_ReportsSecurityViolation()
    {
        using var engine = new ScriptEngine();
        var options = ScriptOptions.Default.WithScriptType(ScriptType.Class);

        var result = await Guard(engine.ExecuteAsync("namespace System.Reflection.Emit { public class Danger { } }", options));

        Assert.False(result.IsSuccess);
        Assert.IsType<ScriptSecurityException>(result.Exception);
        var exception = (ScriptSecurityException)result.Exception!;
        Assert.Equal("RestrictedNamespace", exception.ViolationType);
    }

    /// <summary>
    /// 脚本声明受限类型时被程序集安全检查拦下
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenScriptDeclaresForbiddenType_ReportsSecurityViolation()
    {
        using var engine = new ScriptEngine();
        var options = ScriptOptions.Default.WithScriptType(ScriptType.Class);

        var result = await Guard(engine.ExecuteAsync("namespace System { public class Environment { } }", options));

        Assert.False(result.IsSuccess);
        Assert.IsType<ScriptSecurityException>(result.Exception);
        var exception = (ScriptSecurityException)result.Exception!;
        Assert.Equal("RestrictedType", exception.ViolationType);
    }

    /// <summary>
    /// 超过超时时间的脚本不允许被报告为成功
    /// </summary>
    /// <remarks>
    /// 该用例按超时语义断言：既接受抛出 <see cref="ScriptTimeoutException"/>(会被 <c>ExecuteSafelyAsync</c> 收敛成失败结果)，
    /// 也接受直接返回失败结果，但不接受"睡够 400ms 后照常返回成功"。
    /// 当前实现用 <c>Task.Run(..., token)</c> 承载脚本体，令牌只能阻止任务启动、无法中断已运行的同步代码，
    /// 因此在修复前本用例会失败——已作为疑似缺陷上报，不迁就现状改断言。
    /// </remarks>
    [Fact]
    public async Task ExecuteAsync_WhenScriptExceedsTimeout_DoesNotReportSuccess()
    {
        using var engine = new ScriptEngine();
        var options = ScriptOptions.Default.WithTimeout(50);

        var result = await Guard(engine.ExecuteSafelyAsync("System.Threading.Thread.Sleep(400);", options));

        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // 清理失败不影响用例结论
        }
    }

    /// <summary>
    /// 判断路径是否不含引擎视为危险的字符
    /// </summary>
    /// <param name="path">待判断路径</param>
    /// <returns>不含危险字符返回 true</returns>
    private static bool IsPathFriendly(string path)
    {
        return !path.Contains("..") && !path.Contains('~') && !path.Contains('$');
    }

    /// <summary>
    /// 给真实编译的等待套上硬超时，避免编译器异常时挂死流水线
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="task">待等待的任务</param>
    /// <returns>带超时保护的任务</returns>
    private static Task<T> Guard<T>(Task<T> task)
    {
        return task.WaitAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
    }
}
