// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 审计子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AuditingCapabilities
{
    /// <summary>
    /// 审计：接收器 <c>IAuditSink</c> 与查询 <see cref="IAuditTrail"/>。
    /// </summary>
    public const string Trail = "auditing.trail";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
