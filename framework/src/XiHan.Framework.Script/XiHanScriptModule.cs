#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanScriptModule
// Guid:9dc6a201-2e4d-42ee-af7c-2ba77012b254
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 08:12:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Script;

/// <summary>
/// 曦寒框架脚本引擎模块
/// </summary>
public class XiHanScriptModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
    }
}
