// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.Configuration;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 曦寒应用创建选项测试
/// </summary>
/// <remarks>
/// 这个选项对象是应用装配的唯一入参通道，用例锁的是「哪些属性是只读的、默认值是什么」，
/// 因为默认值一旦漂移会静默改变所有宿主的启动行为。
/// </remarks>
public class XiHanApplicationCreationOptionsTests
{
    /// <summary>
    /// 服务集合为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => new XiHanApplicationCreationOptions(null!));

        Assert.Equal("services", thrown.ParamName);
    }

    /// <summary>
    /// 构造后直接持有传入的服务集合本身，而不是副本
    /// </summary>
    [Fact]
    public void Constructor_KeepsSameServiceCollectionInstance()
    {
        IServiceCollection services = new ServiceCollection();

        var options = new XiHanApplicationCreationOptions(services);

        Assert.Same(services, options.Services);
    }

    /// <summary>
    /// 构造后插件源与配置选项都已就绪，且插件源为空
    /// </summary>
    [Fact]
    public void Constructor_InitializesPlugInSourcesAndConfiguration()
    {
        var options = new XiHanApplicationCreationOptions(new ServiceCollection());

        Assert.NotNull(options.PlugInSources);
        Assert.Empty(options.PlugInSources);
        Assert.IsType<PlugInSourceList>(options.PlugInSources);

        Assert.NotNull(options.Configuration);
        Assert.IsType<XiHanConfigurationBuilderOptions>(options.Configuration);
    }

    /// <summary>
    /// 默认不跳过服务配置，应用名与环境名都为空
    /// </summary>
    [Fact]
    public void Constructor_Defaults_DoNotSkipConfigureServicesAndHaveNoNames()
    {
        var options = new XiHanApplicationCreationOptions(new ServiceCollection());

        Assert.False(options.SkipConfigureServices);
        Assert.Null(options.ApplicationName);
        Assert.Null(options.Environment);
    }

    /// <summary>
    /// 可写属性能被宿主改写
    /// </summary>
    [Fact]
    public void Properties_AreWritable()
    {
        var options = new XiHanApplicationCreationOptions(new ServiceCollection())
        {
            SkipConfigureServices = true,
            ApplicationName = "曦寒测试应用",
            Environment = "Staging"
        };

        Assert.True(options.SkipConfigureServices);
        Assert.Equal("曦寒测试应用", options.ApplicationName);
        Assert.Equal("Staging", options.Environment);
    }

    /// <summary>
    /// 服务集合、插件源与配置选项都是只读引用，宿主只能改内容不能换实例
    /// </summary>
    [Fact]
    public void ReferenceProperties_HaveNoSetter()
    {
        var type = typeof(XiHanApplicationCreationOptions);

        Assert.Null(type.GetProperty(nameof(XiHanApplicationCreationOptions.Services))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(XiHanApplicationCreationOptions.PlugInSources))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(XiHanApplicationCreationOptions.Configuration))!.SetMethod);
    }
}
