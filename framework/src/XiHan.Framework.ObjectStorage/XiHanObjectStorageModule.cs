#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectStorageModule
// Guid:6d0ea7cb-9f7e-4cf6-a01b-6b6c3e11ea74
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 03:37:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.ObjectStorage;

/// <summary>
/// 曦寒对象存储模块
/// </summary>
public class XiHanObjectStorageModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 对象存储提供程序将由使用方根据需求注册
        // 例如：services.AddSingleton<IFileStorageProvider, LocalFileStorageProvider>();
    }
}
