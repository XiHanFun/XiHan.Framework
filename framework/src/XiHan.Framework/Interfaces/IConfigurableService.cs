#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IConfigurableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可配置服务接口
/// </summary>
/// <typeparam name="TConfiguration">配置类型</typeparam>
public interface IConfigurableService<TConfiguration> : IFrameworkService
{
    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="configuration">配置对象</param>
    void Configure(TConfiguration configuration);

    /// <summary>
    /// 获取当前配置
    /// </summary>
    /// <returns>当前配置</returns>
    TConfiguration GetConfiguration();
}
