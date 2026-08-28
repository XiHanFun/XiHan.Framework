// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.Options;
using XiHan.Framework.Core.Options;

namespace XiHan.Framework.Core.Tests.Extensions.Options;

/// <summary>
/// 配置曦寒动态选项管理器扩展方法测试
/// </summary>
/// <remarks>
/// 这两个扩展挂在 <see cref="IOptions{TOptions}"/> 上，但只有动态选项管理器才真的支持"重设"。
/// 挂在通用接口上是为了调用点写起来自然，代价是类型不匹配只能在运行期发现——
/// 因此「不是动态选项就抛框架异常」这条兜底是它唯一的安全网，必须锁死，
/// 同时锁死无名重载走的是空字符串名称（这决定了它改写的是默认命名实例而不是某个具名实例）。
/// </remarks>
public class OptionsXiHanDynamicOptionsManagerExtensionsTests
{
    /// <summary>
    /// 无名重载以空字符串名称回调重写钩子
    /// </summary>
    [Fact]
    public async Task SetAsync_WithoutName_UsesEmptyName()
    {
        var manager = CreateManager();
        IOptions<DynamicSampleOptions> options = manager;

        await options.SetAsync();

        Assert.Equal([string.Empty], manager.OverriddenNames);
    }

    /// <summary>
    /// 具名重载把名称原样传给重写钩子
    /// </summary>
    [Fact]
    public async Task SetAsync_WithName_PassesNameThrough()
    {
        var manager = CreateManager();
        IOptions<DynamicSampleOptions> options = manager;

        await options.SetAsync("租户甲");

        Assert.Equal(["租户甲"], manager.OverriddenNames);
    }

    /// <summary>
    /// 重写钩子拿到的是按配置委托建好的选项实例
    /// </summary>
    [Fact]
    public async Task SetAsync_PassesConfiguredOptionsToOverrideHook()
    {
        var manager = CreateManager();
        IOptions<DynamicSampleOptions> options = manager;

        await options.SetAsync();

        Assert.NotNull(manager.LastOptions);
        Assert.Equal("已覆盖:", manager.LastOptions!.Name);
    }

    /// <summary>
    /// 多次重设按调用顺序累积名称
    /// </summary>
    [Fact]
    public async Task SetAsync_CalledMultipleTimes_AccumulatesNamesInOrder()
    {
        var manager = CreateManager();
        IOptions<DynamicSampleOptions> options = manager;

        await options.SetAsync();
        await options.SetAsync("租户甲");
        await options.SetAsync("租户乙");

        Assert.Equal([string.Empty, "租户甲", "租户乙"], manager.OverriddenNames);
    }

    /// <summary>
    /// 不是动态选项管理器时抛出框架异常并点名基类
    /// </summary>
    [Fact]
    public void SetAsync_OnPlainOptions_ThrowsXiHanException()
    {
        IOptions<DynamicSampleOptions> options = new OptionsWrapper<DynamicSampleOptions>(new DynamicSampleOptions());

        var thrown = Assert.Throws<XiHanException>(() => { _ = options.SetAsync(); });

        Assert.Contains(nameof(XiHanDynamicOptionsManager<DynamicSampleOptions>), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 具名重载在不是动态选项管理器时同样抛出框架异常
    /// </summary>
    [Fact]
    public void SetAsync_WithName_OnPlainOptions_ThrowsXiHanException()
    {
        IOptions<DynamicSampleOptions> options = new OptionsWrapper<DynamicSampleOptions>(new DynamicSampleOptions());

        Assert.Throws<XiHanException>(() => { _ = options.SetAsync("租户甲"); });
    }

    /// <summary>
    /// 异常是同步抛出的，调用方不必等待任务就能拿到
    /// </summary>
    /// <remarks>
    /// 扩展方法不是 async 的，类型不匹配会在返回任务之前就抛出；
    /// 如果哪天改成 async，未 await 的调用点会静默吞掉这个异常，因此把"同步抛出"这条固定下来。
    /// </remarks>
    [Fact]
    public void SetAsync_OnPlainOptions_ThrowsSynchronouslyBeforeReturningTask()
    {
        IOptions<DynamicSampleOptions> options = new OptionsWrapper<DynamicSampleOptions>(new DynamicSampleOptions());
        var reached = false;

        Assert.Throws<XiHanException>(() =>
        {
            _ = options.SetAsync();
            reached = true;
        });

        Assert.False(reached);
    }

    /// <summary>
    /// 建一个已按配置委托初始化过的动态选项管理器
    /// </summary>
    /// <returns>动态选项管理器</returns>
    private static SampleDynamicOptionsManager CreateManager()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddOptions();
        services.Configure<DynamicSampleOptions>(options => options.Name = "初始");

        // 刻意不释放这个容器：选项工厂在构造时就把配置委托取成数组、之后不再回查容器，
        // 但提前释放会让「工厂是否还可用」变成与本用例无关的额外变量。
        var provider = services.BuildServiceProvider();

        return new SampleDynamicOptionsManager(provider.GetRequiredService<IOptionsFactory<DynamicSampleOptions>>());
    }
}

/// <summary>
/// 动态选项测试用的选项
/// </summary>
public sealed class DynamicSampleOptions
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = "默认";
}

/// <summary>
/// 记录重写调用的动态选项管理器
/// </summary>
/// <remarks>
/// 框架的动态选项管理器是抽象类，重写钩子由使用方实现；
/// 这里的最小实现只做记录与改名，用来观察扩展方法把什么名称、什么实例交了下来。
/// </remarks>
public sealed class SampleDynamicOptionsManager : XiHanDynamicOptionsManager<DynamicSampleOptions>
{
    private readonly List<string> _overriddenNames = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="factory">选项工厂</param>
    public SampleDynamicOptionsManager(IOptionsFactory<DynamicSampleOptions> factory)
        : base(factory)
    {
    }

    /// <summary>
    /// 按调用顺序记录的名称
    /// </summary>
    public IReadOnlyList<string> OverriddenNames => _overriddenNames;

    /// <summary>
    /// 最后一次收到的选项实例
    /// </summary>
    public DynamicSampleOptions? LastOptions { get; private set; }

    /// <summary>
    /// 重写选项
    /// </summary>
    /// <param name="name">选项名称</param>
    /// <param name="options">选项实例</param>
    /// <returns>已完成的任务</returns>
    protected override Task OverrideOptionsAsync(string name, DynamicSampleOptions options)
    {
        _overriddenNames.Add(name);
        LastOptions = options;
        options.Name = "已覆盖:" + name;
        return Task.CompletedTask;
    }
}
