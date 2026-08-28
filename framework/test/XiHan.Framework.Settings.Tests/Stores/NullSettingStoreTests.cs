// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Tests.Stores;

/// <summary>
/// 空设置存储测试
/// </summary>
/// <remarks>
/// 这是没有接入持久化时的兜底实现：读取一律落空、写入一律吞掉，且必须以 TryRegister 登记，
/// 一旦丢掉 TryRegister，它会把宿主项目真正的存储实现顶掉，故障表现为"设置怎么写都不生效"。
/// </remarks>
public class NullSettingStoreTests
{
    /// <summary>
    /// 单项读取恒返回 null
    /// </summary>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("G", null)]
    [InlineData("U", "42")]
    public async Task GetOrNullAsync_AlwaysReturnsNull(string? providerName, string? providerKey)
    {
        var store = new NullSettingStore();

        var value = await store.GetOrNullAsync("Foo", providerName, providerKey);

        Assert.Null(value);
    }

    /// <summary>
    /// 批量读取按入参顺序补齐空值条目
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsOneNullValuedEntryPerNameInOrder()
    {
        var store = new NullSettingStore();

        var values = await store.GetAllAsync(["Foo", "Bar", "Baz"], "G", null);

        Assert.Equal(new[] { "Foo", "Bar", "Baz" }, values.Select(x => x.Name).ToArray());
        Assert.All(values, x => Assert.Null(x.Value));
    }

    /// <summary>
    /// 入参为空数组时返回空列表
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenNamesEmpty_ReturnsEmptyList()
    {
        var store = new NullSettingStore();

        var values = await store.GetAllAsync([], "G", null);

        Assert.Empty(values);
    }

    /// <summary>
    /// 写入是空实现，不抛异常也不改变后续读取结果
    /// </summary>
    [Fact]
    public async Task SetAsync_IsNoOpAndKeepsReadsEmpty()
    {
        var store = new NullSettingStore();

        await store.SetAsync("Foo", "bar", "G", null);

        Assert.Null(await store.GetOrNullAsync("Foo", "G", null));
    }

    /// <summary>
    /// 删除是空实现，不抛异常
    /// </summary>
    [Fact]
    public async Task DeleteAsync_IsNoOp()
    {
        var store = new NullSettingStore();

        await store.DeleteAsync("Foo", "G", null);

        Assert.Null(await store.GetOrNullAsync("Foo", "G", null));
    }

    /// <summary>
    /// 日志器默认是空日志器，且可被属性注入替换
    /// </summary>
    [Fact]
    public void Logger_DefaultsToNullLoggerAndIsReplaceable()
    {
        var store = new NullSettingStore();

        Assert.Same(NullLogger<NullSettingStore>.Instance, store.Logger);

        var probe = new ProbeLogger();
        store.Logger = probe;

        Assert.Same(probe, store.Logger);
    }

    /// <summary>
    /// 以 TryRegister 方式登记的单例存储，不会顶掉宿主的真实实现
    /// </summary>
    [Fact]
    public void NullSettingStore_IsTryRegisteredSingletonStore()
    {
        var attribute = typeof(NullSettingStore).GetCustomAttribute<DependencyAttribute>();

        Assert.NotNull(attribute);
        Assert.True(attribute!.TryRegister);
        Assert.True(typeof(ISettingStore).IsAssignableFrom(typeof(NullSettingStore)));
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(NullSettingStore)));
    }

    /// <summary>
    /// 用于验证日志器可被替换的最小日志实现
    /// </summary>
    private sealed class ProbeLogger : ILogger<NullSettingStore>
    {
        /// <summary>
        /// 开启日志范围
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <param name="state">状态</param>
        /// <returns>始终为 null</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <summary>
        /// 是否启用该日志等级
        /// </summary>
        /// <param name="logLevel">日志等级</param>
        /// <returns>始终为 false</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <summary>
        /// 写日志（空实现）
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <param name="logLevel">日志等级</param>
        /// <param name="eventId">事件标识</param>
        /// <param name="state">状态</param>
        /// <param name="exception">异常</param>
        /// <param name="formatter">格式化函数</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
