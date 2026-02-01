#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectStorageModule
// Guid:a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6
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
