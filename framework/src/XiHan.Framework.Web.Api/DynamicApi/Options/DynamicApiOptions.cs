// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.DynamicApi.Options;

/// <summary>
/// 动态 API 配置选项
/// </summary>
public class DynamicApiOptions
{
    /// <summary>
    /// 是否启用动态 API
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 默认路由前缀
    /// </summary>
    public string DefaultRoutePrefix { get; set; } = "api";

    /// <summary>
    /// 默认 API 版本，字符串类型的数字，不需要加 v 前缀
    /// </summary>
    public string? DefaultApiVersion { get; set; }

    /// <summary>
    /// 是否启用 API 版本控制
    /// </summary>
    public bool EnableApiVersioning { get; set; } = true;

    /// <summary>
    /// 是否启用批量操作
    /// </summary>
    public bool EnableBatchOperations { get; set; } = true;

    /// <summary>
    /// 批量操作最大数量
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// 是否移除服务名称的后缀
    /// 例如: UserAppService -> User
    /// </summary>
    public bool RemoveServiceSuffix { get; set; } = true;

    /// <summary>
    /// 要移除的服务名称后缀列表
    /// 例如: UserAppService -> User
    /// </summary>
    public List<string> ServiceSuffixes { get; set; } = ["ApplicationService", "AppService", "Service"];

    /// <summary>
    /// 约定配置
    /// </summary>
    public DynamicApiConventionOptions Conventions { get; set; } = new();

    /// <summary>
    /// 路由配置
    /// </summary>
    public DynamicApiRouteOptions Routes { get; set; } = new();
}
