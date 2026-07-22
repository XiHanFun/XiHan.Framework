// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Configuration;
using XiHan.Framework.Core.Modularity.PlugIns;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒应用创建选项
/// </summary>
public class XiHanApplicationCreationOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="services"></param>
    public XiHanApplicationCreationOptions(IServiceCollection services)
    {
        Services = Guard.NotNull(services, nameof(services));
        PlugInSources = [];
        Configuration = new XiHanConfigurationBuilderOptions();
    }

    /// <summary>
    /// 服务容器
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 插件源列表
    /// </summary>
    public PlugInSourceList PlugInSources { get; }

    /// <summary>
    /// 此属性中的选项仅在未注册 IConfiguration 时生效
    /// </summary>
    public XiHanConfigurationBuilderOptions Configuration { get; }

    /// <summary>
    /// 是否跳过配置服务
    /// </summary>
    public bool SkipConfigureServices { get; set; }

    /// <summary>
    /// 应用名称
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// 环境
    /// </summary>
    public string? Environment { get; set; }
}
