// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 没有显式声明动作时，哪些操作调用要记审计。
/// </summary>
/// <remarks>
/// 显式 <c>SetAuditAction</c> 的调用总是记录。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum AuditSelection
{
    /// <summary>
    /// 只记声明了权限的操作（默认）。
    /// </summary>
    Protected = 0,

    /// <summary>
    /// 记所有操作。
    /// </summary>
    All = 1,

    /// <summary>
    /// 只记显式声明动作的调用。
    /// </summary>
    None = 2,
}
