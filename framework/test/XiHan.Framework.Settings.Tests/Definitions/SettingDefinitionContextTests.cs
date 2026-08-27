// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Tests.Definitions;

/// <summary>
/// 设置定义上下文测试
/// </summary>
/// <remarks>
/// 上下文是各 <c>ISettingDefinitionProvider</c> 汇总定义的收集器，重复名必须当场失败，
/// 否则后写入的定义会静默覆盖前一个，排查成本极高。
/// </remarks>
public class SettingDefinitionContextTests
{
    /// <summary>
    /// 添加后可按名称原样取回同一实例
    /// </summary>
    [Fact]
    public void Add_ThenGetOrNull_ReturnsSameInstance()
    {
        var context = new SettingDefinitionContext();
        var definition = new SettingDefinition("Foo", "bar");

        context.Add(definition);

        Assert.Same(definition, context.GetOrNull("Foo"));
    }

    /// <summary>
    /// 重复名称立即抛出并在消息中点名冲突的设置
    /// </summary>
    [Fact]
    public void Add_WhenNameAlreadyExists_ThrowsXiHanException()
    {
        var context = new SettingDefinitionContext();
        context.Add(new SettingDefinition("Foo"));

        var exception = Assert.Throws<XiHanException>(() => context.Add(new SettingDefinition("Foo")));

        Assert.Contains("Foo", exception.Message);
    }

    /// <summary>
    /// 冲突发生后原有定义保持不变，不会被后来者覆盖
    /// </summary>
    [Fact]
    public void Add_WhenNameAlreadyExists_KeepsTheFirstDefinition()
    {
        var context = new SettingDefinitionContext();
        var original = new SettingDefinition("Foo", "first");
        context.Add(original);

        Assert.Throws<XiHanException>(() => context.Add(new SettingDefinition("Foo", "second")));

        Assert.Same(original, context.GetOrNull("Foo"));
    }

    /// <summary>
    /// 未定义的名称返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public void GetOrNull_WhenNameNotDefined_ReturnsNull()
    {
        var context = new SettingDefinitionContext();

        Assert.Null(context.GetOrNull("Missing"));
    }

    /// <summary>
    /// 新建上下文不含任何定义
    /// </summary>
    [Fact]
    public void GetAll_OnNewContext_ReturnsEmptyDictionary()
    {
        var context = new SettingDefinitionContext();

        Assert.Empty(context.GetAll());
    }

    /// <summary>
    /// 返回的是快照副本，改动它不会污染上下文内部状态
    /// </summary>
    [Fact]
    public void GetAll_ReturnsDetachedSnapshot()
    {
        var context = new SettingDefinitionContext();
        context.Add(new SettingDefinition("Foo"));

        var snapshot = context.GetAll();
        snapshot.Remove("Foo");
        snapshot["Injected"] = new SettingDefinition("Injected");

        Assert.NotNull(context.GetOrNull("Foo"));
        Assert.Null(context.GetOrNull("Injected"));
        Assert.Single(context.GetAll());
    }

    /// <summary>
    /// 取快照之后新增的定义不会回填到旧快照
    /// </summary>
    [Fact]
    public void GetAll_SnapshotDoesNotSeeLaterAdditions()
    {
        var context = new SettingDefinitionContext();
        context.Add(new SettingDefinition("Foo"));

        var snapshot = context.GetAll();
        context.Add(new SettingDefinition("Bar"));

        Assert.Single(snapshot);
        Assert.Equal(2, context.GetAll().Count);
    }

    /// <summary>
    /// 上下文按单例依赖登记
    /// </summary>
    [Fact]
    public void SettingDefinitionContext_IsSingletonDependency()
    {
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(SettingDefinitionContext)));
        Assert.True(typeof(ISettingDefinitionContext).IsAssignableFrom(typeof(SettingDefinitionContext)));
    }
}
