#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTestsIntegrationModule
// Guid:c9bf348b-8c2f-4e2a-9f36-cc2edafe551e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 5:34:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Logging;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Logging;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Tests.Integration;

/// <summary>
/// 曦寒测试应用集成主机
/// </summary>
[DependsOn(
    typeof(XiHanLoggingModule)
    )]
public class XiHanTestsIntegrationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        //var systemInfo = SystemInfoManager.GetSystemInfo();
        //LogHelper.Info(systemInfo.ToJson());
        for (var i = 0; i < 1000000; i++)
        {
            LogFileHelper.Handle($"应用启动中...{i + 1}/1000000");
        }
    }
}
