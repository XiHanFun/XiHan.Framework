#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDistributedIdsModule
// Guid:b1279fc1-b2d1-47ac-b1fa-0550fa4684c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/17 04:37:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds.Extensions.DependencyInjection;

namespace XiHan.Framework.DistributedIds;

/// <summary>
/// 曦寒框架分布式唯一标识生成模块
/// </summary>
public class XiHanDistributedIdsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 注册分布式唯一标识生成器服务
        services.AddXiHanDistributedIds();
    }
}
