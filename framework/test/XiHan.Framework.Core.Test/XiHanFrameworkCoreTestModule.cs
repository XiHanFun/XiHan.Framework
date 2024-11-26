#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFrameworkCoreTestModule
// Guid:449da542-7e67-446f-b3f3-12fdf488cf09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/19 5:55:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Test;

/// <summary>
/// XiHanFrameworkCoreTestModule
/// </summary>
public class XiHanFrameworkCoreTestModule : XiHanModule
{
    /// <summary>
    /// 注册服务
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        _ = context.Services.AddLogging(builder =>
        {
            _ = builder
                .SetMinimumLevel(LogLevel.Information);
        });

        _ = context.Services.AddTransient<MyService>();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 初始化模块
        Console.WriteLine("Application initialized!");
    }
}