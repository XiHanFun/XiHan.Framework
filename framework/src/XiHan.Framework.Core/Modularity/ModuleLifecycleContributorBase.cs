#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ModuleLifecycleContributorBase
// Guid:bb4fe156-3e4c-4877-a4b5-4948a2835654
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:20:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块生命周期贡献者基类
/// </summary>
public abstract class ModuleLifecycleContributorBase : IModuleLifecycleContributor
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public virtual Task InitializeAsync([NotNull] ApplicationInitializationContext context, [NotNull] IXiHanModule module)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public virtual void Initialize([NotNull] ApplicationInitializationContext context, [NotNull] IXiHanModule module)
    {
    }

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public virtual Task ShutdownAsync([NotNull] ApplicationShutdownContext context, [NotNull] IXiHanModule module)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public virtual void Shutdown([NotNull] ApplicationShutdownContext context, [NotNull] IXiHanModule module)
    {
    }
}