#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotSmsModule
// Guid:96812f7c-45cc-439b-8906-4981a95b853b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Sms.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.Sms;

/// <summary>
/// 曦寒框架机器人短信模块
/// </summary>
[DependsOn(
    typeof(XiHanBotModule)
    )]
public class XiHanBotSmsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanBotSms();
    }
}
