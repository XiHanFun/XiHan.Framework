#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTestsWebModule
// Guid:c9bf348b-8c2f-4e2a-9f36-cc2edafe551e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 5:34:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI;
using XiHan.Framework.Application;
using XiHan.Framework.Authentication;
using XiHan.Framework.Authorization;
using XiHan.Framework.Bot;
using XiHan.Framework.Caching;
using XiHan.Framework.CodeGeneration;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain;
using XiHan.Framework.EventBus;
using XiHan.Framework.Http;
using XiHan.Framework.Localization;
using XiHan.Framework.Logging;
using XiHan.Framework.Messaging;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.ObjectMapping;
using XiHan.Framework.Script;
using XiHan.Framework.SearchEngines;
using XiHan.Framework.Security;
using XiHan.Framework.Serialization;
using XiHan.Framework.Settings;
using XiHan.Framework.Tasks;
using XiHan.Framework.Templating;
using XiHan.Framework.Threading;
using XiHan.Framework.Uow;
using XiHan.Framework.Validation;
using XiHan.Framework.VirtualFileSystem;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Docs;
using XiHan.Framework.Web.Gateway;
using XiHan.Framework.Web.RealTime;

namespace XiHan.Framework.Web.Tests;

/// <summary>
/// 曦寒测试应用 Web 主机
/// </summary>
[DependsOn(
    typeof(XiHanAIModule),
    typeof(XiHanAuthenticationModule),
    typeof(XiHanAuthorizationModule),
    typeof(XiHanTasksModule),
    typeof(XiHanBotModule),
    typeof(XiHanCachingModule),
    typeof(XiHanCodeGenerationModule),
    typeof(XiHanDataModule),
    typeof(XiHanDomainModule),
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanEventBusModule),
    typeof(XiHanHttpModule),
    typeof(XiHanLocalizationModule),
    typeof(XiHanLoggingModule),
    typeof(XiHanMessagingModule),
    typeof(XiHanMultiTenancyModule),
    typeof(XiHanObjectMappingModule),
    typeof(XiHanScriptModule),
    typeof(XiHanSearchEnginesModule),
    typeof(XiHanSecurityModule),
    typeof(XiHanSerializationModule),
    typeof(XiHanSettingsModule),
    typeof(XiHanTemplatingModule),
    typeof(XiHanThreadingModule),
    typeof(XiHanUowModule),
    typeof(XiHanValidationModule),
    typeof(XiHanVirtualFileSystemModule),
    typeof(XiHanApplicationModule),
    typeof(XiHanWebCoreModule),
    typeof(XiHanWebApiModule),
    typeof(XiHanWebDocsModule),
    typeof(XiHanWebRealTimeModule),
    typeof(XiHanWebGatewayModule)
)]
public class XiHanTestsWebModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }
}
