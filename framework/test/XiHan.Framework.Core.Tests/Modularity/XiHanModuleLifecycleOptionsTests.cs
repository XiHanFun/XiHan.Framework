// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 曦寒模块生命周期选项测试
/// </summary>
/// <remarks>
/// 贡献者列表的顺序就是应用生命周期阶段的执行顺序，模块管理器按列表顺序逐阶段推进，
/// 因此这里锁死「默认为空、按加入顺序保留、类型受契约约束」三点。
/// </remarks>
public class XiHanModuleLifecycleOptionsTests
{
    /// <summary>
    /// 默认贡献者列表为空
    /// </summary>
    [Fact]
    public void Contributors_DefaultsToEmpty()
    {
        var options = new XiHanModuleLifecycleOptions();

        Assert.Empty(options.Contributors);
    }

    /// <summary>
    /// 贡献者按加入顺序保留
    /// </summary>
    [Fact]
    public void Contributors_KeepsInsertionOrder()
    {
        var options = new XiHanModuleLifecycleOptions();

        options.Contributors.Add<OnPreApplicationInitializationModuleLifecycleContributor>();
        options.Contributors.Add<OnApplicationInitializationModuleLifecycleContributor>();
        options.Contributors.Add<OnPostApplicationInitializationModuleLifecycleContributor>();

        Assert.Equal(3, options.Contributors.Count);
        Assert.Equal(typeof(OnPreApplicationInitializationModuleLifecycleContributor), options.Contributors[0]);
        Assert.Equal(typeof(OnApplicationInitializationModuleLifecycleContributor), options.Contributors[1]);
        Assert.Equal(typeof(OnPostApplicationInitializationModuleLifecycleContributor), options.Contributors[2]);
    }

    /// <summary>
    /// 尝试添加已存在的贡献者时不重复登记
    /// </summary>
    [Fact]
    public void Contributors_TryAdd_IsIdempotent()
    {
        var options = new XiHanModuleLifecycleOptions();

        Assert.True(options.Contributors.TryAdd<OnApplicationShutdownModuleLifecycleContributor>());
        Assert.False(options.Contributors.TryAdd<OnApplicationShutdownModuleLifecycleContributor>());
        Assert.Single(options.Contributors);
    }

    /// <summary>
    /// 添加非贡献者类型时抛出
    /// </summary>
    [Fact]
    public void Contributors_WhenTypeIsNotContributor_Throws()
    {
        var options = new XiHanModuleLifecycleOptions();

        Assert.Throws<ArgumentException>(() => options.Contributors.Add(typeof(string)));
    }

    /// <summary>
    /// 移除贡献者后列表随之收缩
    /// </summary>
    [Fact]
    public void Contributors_Remove_DropsRegisteredType()
    {
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<OnApplicationShutdownModuleLifecycleContributor>();

        options.Contributors.Remove<OnApplicationShutdownModuleLifecycleContributor>();

        Assert.Empty(options.Contributors);
    }
}
