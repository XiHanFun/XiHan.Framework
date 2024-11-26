﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IModuleLoader
// Guid:9634e9a4-e9b1-41ce-8756-ed14363a8d11
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:09:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块加载器接口
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// 加载模块
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="plugInSources"></param>
    /// <returns></returns>
    IModuleDescriptor[] LoadModules([NotNull] IServiceCollection services, [NotNull] Type startupModuleType, [NotNull] PlugInSourceList plugInSources);
}