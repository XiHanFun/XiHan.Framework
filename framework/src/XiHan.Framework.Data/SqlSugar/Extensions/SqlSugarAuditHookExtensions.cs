#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarAuditHookExtensions
// Guid:d9e8f7a6-b5c4-43d2-80f1-c9f8e7d6c5b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Data.Auditing;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// SqlSugar 审计钩子扩展
/// </summary>
/// <remarks>
/// 提供 <c>UseEntityChangeInterceptor</c> 扩展方法，将 <see cref="EntityChangeInterceptor"/>
/// 挂载到 SqlSugar 客户端的命令级 AOP 事件上（OnLogExecuting / OnLogExecuted），
/// 实现对所有 INSERT / UPDATE / DELETE 操作的自动差异日志记录。
/// <para>
/// 注意：由于 SqlSugar 的 <c>OnLogExecuting</c> / <c>OnLogExecuted</c> 为仅写属性，
/// 调用此方法将覆盖现有处理器。框架内部已在 <c>SetSugarAop</c> 中自动调用拦截器，
/// 一般无需手动调用此扩展方法。
/// 仅在绕过 <c>AddXiHanDataSqlSugar</c> 手动创建 SqlSugar 客户端时需要。
/// </para>
/// </remarks>
public static class SqlSugarAuditHookExtensions
{
    /// <summary>
    /// 为 SqlSugar 客户端挂载实体变更拦截器
    /// </summary>
    /// <param name="client">SqlSugar 客户端</param>
    /// <param name="interceptor">实体变更拦截器实例</param>
    /// <remarks>
    /// 使用 <c>=</c> 直接赋值（SqlSugar AOP 属性仅写），覆盖已有的日志处理器。
    /// 大多数场景应通过 <see cref="XiHanDataModule"/> 自动集成，而非手动调用此方法。
    /// </remarks>
    public static void UseEntityChangeInterceptor(this ISqlSugarClient client, EntityChangeInterceptor interceptor)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (interceptor is null)
        {
            throw new ArgumentNullException(nameof(interceptor));
        }

        // SqlSugarScopeProvider 持有 Aop 实例，通过具体类型访问
        if (client is SqlSugarScopeProvider provider)
        {
            provider.Aop.OnLogExecuting = (sql, pars) =>
            {
                interceptor.OnDataExecuting(sql, pars);
            };

            provider.Aop.OnLogExecuted = (sql, pars) =>
            {
                interceptor.OnDataExecuted(sql, pars);
            };
        }
        else
        {
            // 对其他 ISqlSugarClient 实现尝试通过反射访问 Aop
            TryAttachViaReflection(client, interceptor);
        }
    }

    /// <summary>
    /// 反射方式尝试挂载（兜底方案，用于非标准 ISqlSugarClient 实现）
    /// </summary>
    private static void TryAttachViaReflection(ISqlSugarClient client, EntityChangeInterceptor interceptor)
    {
        try
        {
            var aopProperty = client.GetType().GetProperty("Aop");
            if (aopProperty?.GetValue(client) is not AopProvider aop)
            {
                return;
            }

            aop.OnLogExecuting = (sql, pars) =>
            {
                interceptor.OnDataExecuting(sql, pars);
            };

            aop.OnLogExecuted = (sql, pars) =>
            {
                interceptor.OnDataExecuted(sql, pars);
            };
        }
        catch
        {
            // 反射挂载失败静默忽略，不影响主流程
        }
    }
}
