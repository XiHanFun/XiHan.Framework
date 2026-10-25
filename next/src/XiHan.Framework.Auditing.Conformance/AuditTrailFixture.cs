// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Conformance;

/// <summary>
/// 审计套件的夹具：被测接收器与查询（通常是同一个对象）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class AuditTrailFixture
{
    /// <summary>
    /// 被测接收器。
    /// </summary>
    public abstract IAuditSink Sink { get; }

    /// <summary>
    /// 被测查询。
    /// </summary>
    public abstract IAuditTrail Trail { get; }
}
