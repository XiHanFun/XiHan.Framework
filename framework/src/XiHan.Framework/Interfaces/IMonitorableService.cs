#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMonitorableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可监控服务接口
/// </summary>
public interface IMonitorableService : IFrameworkService
{
    /// <summary>
    /// 获取性能指标
    /// </summary>
    /// <returns>性能指标</returns>
    Task<object> GetMetricsAsync();

    /// <summary>
    /// 获取健康状态
    /// </summary>
    /// <returns>健康状态</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// 获取诊断信息
    /// </summary>
    /// <returns>诊断信息</returns>
    Task<string> GetDiagnosticsAsync();
}
