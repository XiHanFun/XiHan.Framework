#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFrameworkService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 框架服务接口
/// </summary>
public interface IFrameworkService
{
    /// <summary>
    /// 服务名称
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// 服务版本
    /// </summary>
    string ServiceVersion { get; }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 初始化服务
    /// </summary>
    /// <returns>初始化结果</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// 启动服务
    /// </summary>
    /// <returns>启动结果</returns>
    Task<bool> StartAsync();

    /// <summary>
    /// 停止服务
    /// </summary>
    /// <returns>停止结果</returns>
    Task<bool> StopAsync();

    /// <summary>
    /// 获取服务状态
    /// </summary>
    /// <returns>服务状态</returns>
    Task<string> GetStatusAsync();
}
