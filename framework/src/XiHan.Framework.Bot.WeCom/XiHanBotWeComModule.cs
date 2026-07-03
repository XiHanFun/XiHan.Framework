#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotWeComModule
// Guid:987bb990-03d8-43d6-acec-c69087b6b3aa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.WeCom.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.WeCom;

/// <summary>
/// 曦寒框架机器人企业微信模块
/// </summary>
[DependsOn(
    typeof(XiHanBotModule)
    )]
public class XiHanBotWeComModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBotWeCom();
    }
}
