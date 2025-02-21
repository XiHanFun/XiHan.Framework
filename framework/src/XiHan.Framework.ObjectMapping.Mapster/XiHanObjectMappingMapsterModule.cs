#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectMappingMapsterModule
// Guid:38efc1bd-18df-499d-bf40-16c29b603ec1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/22 4:40:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Mapster;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.ObjectMapping.Mapster;

/// <summary>
/// 曦寒框架对象映射模块
/// </summary>
public class XiHanObjectMappingMapsterModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddMapster();
    }
}
