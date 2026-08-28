// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Options;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务容器预配置扩展方法测试
/// </summary>
/// <remarks>
/// 预配置是模块之间「先说后做」的机制：前置模块登记委托，后置模块在真正建选项时执行它们。
/// 三条契约：委托列表按选项类型隔离；同一类型多次获取拿到同一个列表（否则前置模块登记的委托会丢）；
/// 执行时按登记顺序依次作用在同一个选项实例上（顺序决定了后登记的能覆盖先登记的）。
/// </remarks>
public class ServiceCollectionPreConfigureExtensionsTests
{
    /// <summary>
    /// 同一选项类型多次获取拿到同一个委托列表
    /// </summary>
    [Fact]
    public void GetPreConfigureActions_ReturnsSameListAcrossCalls()
    {
        IServiceCollection services = new ServiceCollection();

        var first = services.GetPreConfigureActions<PreConfigureSampleOptions>();
        var second = services.GetPreConfigureActions<PreConfigureSampleOptions>();

        Assert.Same(first, second);
    }

    /// <summary>
    /// 首次获取时列表为空
    /// </summary>
    [Fact]
    public void GetPreConfigureActions_InitiallyEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Empty(services.GetPreConfigureActions<PreConfigureSampleOptions>());
    }

    /// <summary>
    /// 登记预配置委托后进入列表，且方法返回同一个服务集合
    /// </summary>
    [Fact]
    public void PreConfigure_AppendsActionAndReturnsSameCollection()
    {
        IServiceCollection services = new ServiceCollection();

        var returned = services.PreConfigure<PreConfigureSampleOptions>(options => options.Name = "甲");

        Assert.Same(services, returned);
        Assert.Single(services.GetPreConfigureActions<PreConfigureSampleOptions>());
    }

    /// <summary>
    /// 执行预配置时新建选项实例并按登记顺序依次作用
    /// </summary>
    [Fact]
    public void ExecutePreConfiguredActions_CreatesInstanceAndAppliesActionsInOrder()
    {
        IServiceCollection services = new ServiceCollection();
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Steps.Add("甲"));
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Steps.Add("乙"));
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Name = "最终");

        var options = services.ExecutePreConfiguredActions<PreConfigureSampleOptions>();

        Assert.Equal(new[] { "甲", "乙" }, options.Steps);
        Assert.Equal("最终", options.Name);
    }

    /// <summary>
    /// 传入实例执行预配置时作用在该实例上并原样返回
    /// </summary>
    [Fact]
    public void ExecutePreConfiguredActions_WithInstance_MutatesAndReturnsSameInstance()
    {
        IServiceCollection services = new ServiceCollection();
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Name = "已预配置");
        PreConfigureSampleOptions target = new();

        var returned = services.ExecutePreConfiguredActions(target);

        Assert.Same(target, returned);
        Assert.Equal("已预配置", target.Name);
    }

    /// <summary>
    /// 没有登记任何委托时执行预配置只得到一个默认实例
    /// </summary>
    [Fact]
    public void ExecutePreConfiguredActions_WithoutActions_ReturnsUntouchedOptions()
    {
        IServiceCollection services = new ServiceCollection();

        var options = services.ExecutePreConfiguredActions<PreConfigureSampleOptions>();

        Assert.Equal("默认", options.Name);
        Assert.Empty(options.Steps);
    }

    /// <summary>
    /// 每次执行预配置都得到一个新的选项实例
    /// </summary>
    /// <remarks>
    /// 委托列表是共享的，选项实例不能共享：两个模块各自执行一遍预配置时若拿到同一个实例，
    /// 后者会看到前者的改动，模块之间的隔离就没了。
    /// </remarks>
    [Fact]
    public void ExecutePreConfiguredActions_ReturnsFreshInstanceEachTime()
    {
        IServiceCollection services = new ServiceCollection();
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Steps.Add("甲"));

        var first = services.ExecutePreConfiguredActions<PreConfigureSampleOptions>();
        var second = services.ExecutePreConfiguredActions<PreConfigureSampleOptions>();

        Assert.NotSame(first, second);
        Assert.Single(first.Steps);
        Assert.Single(second.Steps);
    }

    /// <summary>
    /// 不同选项类型的预配置委托互不干扰
    /// </summary>
    [Fact]
    public void PreConfigureActions_AreIsolatedPerOptionsType()
    {
        IServiceCollection services = new ServiceCollection();
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Name = "甲");

        Assert.Single(services.GetPreConfigureActions<PreConfigureSampleOptions>());
        Assert.Empty(services.GetPreConfigureActions<OtherPreConfigureSampleOptions>());

        var other = services.ExecutePreConfiguredActions<OtherPreConfigureSampleOptions>();

        Assert.Equal("其他默认", other.Marker);
    }

    /// <summary>
    /// 委托列表以对象访问器形式登记，重复获取不会触发重复登记的异常
    /// </summary>
    /// <remarks>
    /// 对象访问器同类型只允许登记一次，重复登记会抛异常；
    /// 预配置列表被每个模块反复获取，因此这里的"先查后建"守卫必须生效。
    /// </remarks>
    [Fact]
    public void GetPreConfigureActions_RepeatedCalls_DoNotThrowFromDuplicateAccessor()
    {
        IServiceCollection services = new ServiceCollection();

        services.GetPreConfigureActions<PreConfigureSampleOptions>();
        services.GetPreConfigureActions<PreConfigureSampleOptions>();
        services.PreConfigure<PreConfigureSampleOptions>(options => options.Name = "甲");

        var accessorDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(ObjectAccessor<PreConfigureActionList<PreConfigureSampleOptions>>))
            .ToArray();

        Assert.Single(accessorDescriptors);
    }
}

/// <summary>
/// 预配置测试用的选项
/// </summary>
public sealed class PreConfigureSampleOptions
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = "默认";

    /// <summary>
    /// 按执行顺序记录的步骤
    /// </summary>
    public List<string> Steps { get; } = [];
}

/// <summary>
/// 预配置测试用的另一种选项
/// </summary>
public sealed class OtherPreConfigureSampleOptions
{
    /// <summary>
    /// 标记
    /// </summary>
    public string Marker { get; set; } = "其他默认";
}
