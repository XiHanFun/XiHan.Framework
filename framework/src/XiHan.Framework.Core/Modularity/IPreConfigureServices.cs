#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPreConfigureServices
// Guid:c52bb2d1-7eff-4e32-a495-26383502c028
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:44:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 服务配置前接口
/// </summary>
public interface IPreConfigureServices
{
    /// <summary>
    /// 服务配置前，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task PreConfigureServicesAsync(ServiceConfigurationContext context);

    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    void PreConfigureServices(ServiceConfigurationContext context);
}
