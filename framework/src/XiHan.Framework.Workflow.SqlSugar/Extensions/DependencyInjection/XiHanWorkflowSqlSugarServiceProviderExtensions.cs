// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Workflow.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 工作流 SqlSugar 存储的服务提供者扩展
/// </summary>
public static class XiHanWorkflowSqlSugarServiceProviderExtensions
{
    /// <summary>
    /// 解析出的分布式锁只在进程内互斥时记录一条警告
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>记录了警告返回 true</returns>
    public static bool WarnIfWorkflowLockIsProcessLocal(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (serviceProvider.GetService<IDistributedLock>() is not DefaultDistributedLock)
        {
            return false;
        }

        serviceProvider.GetService<ILoggerFactory>()?
            .CreateLogger(typeof(XiHanWorkflowSqlSugarModule))
            .LogWarning(
                "当前分布式锁为 {LockType}，只在进程内互斥。工作流以多实例部署时请配置 Redis 分布式锁：" +
                "同一书签的重复恢复已由书签存储拦截，但同一实例的不同书签被多个节点同时恢复时仍会相互覆盖。",
                nameof(DefaultDistributedLock));

        return true;
    }
}
