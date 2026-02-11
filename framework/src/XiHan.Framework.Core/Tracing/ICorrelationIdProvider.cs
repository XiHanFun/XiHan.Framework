#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICorrelationIdProvider
// Guid:3e9ff126-b84f-442f-9385-572cabdb47a7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 08:34:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Tracing;

/// <summary>
/// 关联标识符提供程序接口
/// 定义在分布式系统中管理关联唯一标识的契约，用于跨服务的请求跟踪和日志关联
/// 支持获取当前关联唯一标识和临时切换关联唯一标识上下文
/// </summary>
/// <remarks>
/// 关联唯一标识是分布式跟踪的核心概念，用于：
/// <list type="bullet">
/// <item>跨多个服务调用链的请求跟踪</item>
/// <item>日志聚合和问题排查</item>
/// <item>性能监控和调用链分析</item>
/// <item>分布式事务的上下文传递</item>
/// </list>
/// </remarks>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// 获取当前的关联标识符
    /// 返回当前请求或操作上下文中的关联唯一标识
    /// </summary>
    /// <returns>
    /// 当前的关联标识符字符串，如果当前上下文中没有设置关联唯一标识则返回 null
    /// </returns>
    /// <remarks>
    /// 此方法通常用于：
    /// <list type="bullet">
    /// <item>在日志记录中包含关联唯一标识</item>
    /// <item>向下游服务传递关联唯一标识</item>
    /// <item>在异常处理中记录关联信息</item>
    /// </list>
    /// </remarks>
    string? Get();

    /// <summary>
    /// 临时更改关联标识符
    /// 创建一个作用域，在该作用域内临时设置指定的关联唯一标识
    /// 当返回的 IDisposable 对象被释放时，关联唯一标识将恢复到之前的值
    /// </summary>
    /// <param name="correlationId">
    /// 要设置的关联标识符，传入 null 表示清除当前关联唯一标识
    /// </param>
    /// <returns>
    /// 用于恢复关联唯一标识上下文的释放器对象
    /// </returns>
    /// <remarks>
    /// 使用 using 语句确保在作用域结束时正确恢复关联唯一标识：
    /// <code>
    /// using (correlationIdProvider.Change("new-correlation-id"))
    /// {
    ///     // 在此作用域内，关联唯一标识被设置为 "new-correlation-id"
    ///     // 调用其他服务或记录日志时会使用这个唯一标识
    /// }
    /// // 作用域结束后，关联唯一标识自动恢复到之前的值
    /// </code>
    ///
    /// 常用场景：
    /// <list type="bullet">
    /// <item>在事件处理中传递原始请求的关联唯一标识</item>
    /// <item>在后台任务中设置特定的跟踪标识</item>
    /// <item>在测试环境中模拟特定的关联唯一标识</item>
    /// </list>
    /// </remarks>
    IDisposable Change(string? correlationId);
}
