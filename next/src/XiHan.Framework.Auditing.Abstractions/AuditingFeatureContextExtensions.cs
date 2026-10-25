// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 审计 Provider 装配时的公共步骤。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AuditingFeatureContextExtensions
{
    /// <summary>
    /// 注册消息目录与选项（从 <c>XiHan:Auditing</c> 读取），并把 <see cref="AuditMiddleware"/> 挂到 Observe 阶段末尾。被选中提供 <c>auditing.trail</c> 的 Provider 在注册接收器后调用一次。
    /// </summary>
    public static FeatureContext AddAuditing(this FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(AuditingMessages.Catalog);
        context.Services.AddSingleton(AuditingOptions.FromConfiguration(context.Configuration));
        return context.AddMiddleware<AuditMiddleware>(PipelineStage.Observe, Placement.End);
    }
}
