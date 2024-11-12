#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPostConfigureServices
// Guid:7a016e46-e308-44db-a97b-59aca94df573
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:45:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 服务配置后接口
/// </summary>
public interface IPostConfigureServices
{
    /// <summary>
    /// 服务配置后，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task PostConfigureServicesAsync(ServiceConfigurationContext context);

    /// <summary>
    /// 服务配置后
    /// </summary>
    /// <param name="context"></param>
    void PostConfigureServices(ServiceConfigurationContext context);
}
