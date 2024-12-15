#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IModuleLifecycleContributor
// Guid:f6a54859-750f-4f20-b562-2887fd6e84f7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:19:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块生命周期贡献者接口
/// </summary>
public interface IModuleLifecycleContributor : ITransientDependency
{
    /// <summary>
    /// 初始化，异步
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module);

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    void Initialize(ApplicationInitializationContext context, IXiHanModule module);

    /// <summary>
    /// 关闭，异步
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    Task ShutdownAsync(ApplicationShutdownContext context, IXiHanModule module);

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    void Shutdown(ApplicationShutdownContext context, IXiHanModule module);
}
