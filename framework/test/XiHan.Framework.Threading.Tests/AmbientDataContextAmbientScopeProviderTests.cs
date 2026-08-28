// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace XiHan.Framework.Threading.Tests;

/// <summary>
/// 环境数据上下文作用域提供者测试
/// </summary>
/// <remarks>
/// 覆盖嵌套作用域的进入与逐层还原、跨 await 的传播、并行分支隔离，
/// 以及释放顺序错乱、重复释放这两种调用方误用下的降级契约：不得抛异常，也不得暴露已失效的值。
/// 作用域项字典按封闭泛型共享，因此每个用例都用随机上下文键。
/// </remarks>
public class AmbientDataContextAmbientScopeProviderTests
{
    /// <summary>
    /// 构造时数据上下文为空必须抛出参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenDataContextIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new AmbientDataContextAmbientScopeProvider<string>(null!));

        Assert.Equal("dataContext", exception.ParamName);
    }

    /// <summary>
    /// 日志器默认为空日志器，无需外部注入即可使用
    /// </summary>
    [Fact]
    public void Logger_ByDefault_IsNullLogger()
    {
        var provider = CreateProvider();

        Assert.Same(NullLogger<AmbientDataContextAmbientScopeProvider<string>>.Instance, provider.Logger);
    }

    /// <summary>
    /// 日志器可由外部替换
    /// </summary>
    [Fact]
    public void Logger_WhenAssigned_KeepsGivenInstance()
    {
        var provider = CreateProvider();
        var logger = new NoopLogger();

        provider.Logger = logger;

        Assert.Same(logger, provider.Logger);
    }

    /// <summary>
    /// 没有任何作用域时取值返回默认值
    /// </summary>
    [Fact]
    public void GetValue_WithoutScope_ReturnsDefault()
    {
        var provider = CreateProvider();

        Assert.Null(provider.GetValue(NewKey()));
    }

    /// <summary>
    /// 进入作用域后取到该作用域的值，释放后回到默认值
    /// </summary>
    [Fact]
    public void BeginScope_ThenGetValue_ReturnsScopedValue()
    {
        var provider = CreateProvider();
        var key = NewKey();

        using (provider.BeginScope(key, "外层"))
        {
            Assert.Equal("外层", provider.GetValue(key));
        }

        Assert.Null(provider.GetValue(key));
    }

    /// <summary>
    /// 嵌套作用域内层生效，逐层释放后逐层还原
    /// </summary>
    [Fact]
    public void BeginScope_Nested_InnerWinsAndOuterIsRestoredOnDispose()
    {
        var provider = CreateProvider();
        var key = NewKey();

        using (provider.BeginScope(key, "外层"))
        {
            Assert.Equal("外层", provider.GetValue(key));

            using (provider.BeginScope(key, "中层"))
            {
                Assert.Equal("中层", provider.GetValue(key));

                using (provider.BeginScope(key, "内层"))
                {
                    Assert.Equal("内层", provider.GetValue(key));
                }

                Assert.Equal("中层", provider.GetValue(key));
            }

            Assert.Equal("外层", provider.GetValue(key));
        }

        Assert.Null(provider.GetValue(key));
    }

    /// <summary>
    /// 重复释放同一作用域是幂等的，不得把外层的值一并清掉
    /// </summary>
    [Fact]
    public void Dispose_Twice_IsIdempotent()
    {
        var provider = CreateProvider();
        var key = NewKey();

        using (provider.BeginScope(key, "外层"))
        {
            var inner = provider.BeginScope(key, "内层");

            inner.Dispose();
            Assert.Equal("外层", provider.GetValue(key));

            inner.Dispose();
            Assert.Equal("外层", provider.GetValue(key));
        }

        Assert.Null(provider.GetValue(key));
    }

    /// <summary>
    /// 释放顺序错乱时不抛异常，也不会把已失效的外层值重新暴露出来
    /// </summary>
    /// <remarks>
    /// 先释放外层再释放内层属于调用方误用。这里锁定的是降级契约本身：
    /// 外层一旦释放，取值立刻回落到默认值；之后内层再释放，也不会把已被移除的外层值找回来。
    /// </remarks>
    [Fact]
    public void Dispose_OutOfOrder_DoesNotThrowAndFallsBackToDefault()
    {
        var provider = CreateProvider();
        var key = NewKey();

        var outer = provider.BeginScope(key, "外层");
        var inner = provider.BeginScope(key, "内层");

        outer.Dispose();
        Assert.Null(provider.GetValue(key));

        inner.Dispose();
        Assert.Null(provider.GetValue(key));
    }

    /// <summary>
    /// 作用域值跨 await 传播到续体与子任务
    /// </summary>
    [Fact]
    public async Task BeginScope_AcrossAwait_FlowsIntoContinuationAndChildTask()
    {
        var provider = CreateProvider();
        var key = NewKey();

        using (provider.BeginScope(key, "跨越等待"))
        {
            await Task.Yield();
            Assert.Equal("跨越等待", provider.GetValue(key));

            var observedInChild = await Task.Run(() => provider.GetValue(key), TestContext.Current.CancellationToken);
            Assert.Equal("跨越等待", observedInChild);
        }

        Assert.Null(provider.GetValue(key));
    }

    /// <summary>
    /// 并行分支各自持有独立的作用域，互不串值，也不回灌到父流
    /// </summary>
    [Fact]
    public async Task BeginScope_InParallelTasks_IsIsolatedPerBranch()
    {
        var provider = CreateProvider();
        var key = NewKey();

        var observed = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            using (provider.BeginScope(key, $"分支-{index}"))
            {
                await Task.Yield();
                return provider.GetValue(key);
            }
        }, TestContext.Current.CancellationToken)));

        for (var index = 0; index < observed.Length; index++)
        {
            Assert.Equal($"分支-{index}", observed[index]);
        }

        Assert.Null(provider.GetValue(key));
    }

    /// <summary>
    /// 值类型泛型参数在作用域外返回该类型的默认值
    /// </summary>
    [Fact]
    public void GetValue_ForValueType_ReturnsDefaultOutsideScope()
    {
        var provider = new AmbientDataContextAmbientScopeProvider<int>(new AsyncLocalAmbientDataContext());
        var key = NewKey();

        Assert.Equal(0, provider.GetValue(key));

        using (provider.BeginScope(key, 42))
        {
            Assert.Equal(42, provider.GetValue(key));
        }

        Assert.Equal(0, provider.GetValue(key));
    }

    /// <summary>
    /// 进入作用域时把作用域标识写进环境数据上下文
    /// </summary>
    [Fact]
    public void BeginScope_WritesScopeIdIntoDataContext()
    {
        var dataContext = new RecordingAmbientDataContext();
        var provider = new AmbientDataContextAmbientScopeProvider<string>(dataContext);
        var key = NewKey();

        using (provider.BeginScope(key, "外层"))
        {
            Assert.Single(dataContext.Writes);

            var write = dataContext.Writes[0];
            Assert.Equal(key, write.Key);

            var scopeId = write.Value as string;
            Assert.NotNull(scopeId);
            Assert.True(Guid.TryParse(scopeId, out _));
            Assert.Equal(scopeId, dataContext.GetData(key) as string);
        }
    }

    /// <summary>
    /// 内层释放时把外层作用域标识写回上下文，最外层释放时写回空值
    /// </summary>
    [Fact]
    public void Dispose_WritesOuterScopeIdBackIntoDataContext()
    {
        var dataContext = new RecordingAmbientDataContext();
        var provider = new AmbientDataContextAmbientScopeProvider<string>(dataContext);
        var key = NewKey();

        var outer = provider.BeginScope(key, "外层");
        var outerScopeId = dataContext.GetData(key);
        var inner = provider.BeginScope(key, "内层");
        var innerScopeId = dataContext.GetData(key);

        Assert.NotNull(outerScopeId);
        Assert.NotNull(innerScopeId);
        Assert.NotEqual(outerScopeId, innerScopeId);

        inner.Dispose();
        Assert.Equal(outerScopeId, dataContext.GetData(key));

        outer.Dispose();
        Assert.Null(dataContext.GetData(key));
    }

    /// <summary>
    /// 创建一个基于真实异步本地上下文的字符串作用域提供者
    /// </summary>
    private static AmbientDataContextAmbientScopeProvider<string> CreateProvider()
    {
        return new AmbientDataContextAmbientScopeProvider<string>(new AsyncLocalAmbientDataContext());
    }

    /// <summary>
    /// 生成一个仅本用例使用的随机上下文键
    /// </summary>
    private static string NewKey()
    {
        return "XiHan.Framework.Threading.Tests." + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 记录写入序列的环境数据上下文替身
    /// </summary>
    private sealed class RecordingAmbientDataContext : IAmbientDataContext
    {
        private readonly Dictionary<string, object?> _store = [];

        /// <summary>
        /// 按顺序记录的写入序列
        /// </summary>
        public List<KeyValuePair<string, object?>> Writes { get; } = [];

        /// <summary>
        /// 设置数据
        /// </summary>
        public void SetData(string key, object? value)
        {
            _store[key] = value;
            Writes.Add(new KeyValuePair<string, object?>(key, value));
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        public object? GetData(string key)
        {
            return _store.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>
    /// 什么都不做的日志器替身，仅用于验证日志器属性可被替换
    /// </summary>
    private sealed class NoopLogger : ILogger<AmbientDataContextAmbientScopeProvider<string>>
    {
        /// <summary>
        /// 开始日志作用域
        /// </summary>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <summary>
        /// 是否启用指定日志级别
        /// </summary>
        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
