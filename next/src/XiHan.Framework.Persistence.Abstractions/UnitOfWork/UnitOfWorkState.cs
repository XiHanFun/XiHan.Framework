// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 工作单元状态。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum UnitOfWorkState
{
    /// <summary>
    /// 尚未开始。
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// 进行中。
    /// </summary>
    Active = 1,

    /// <summary>
    /// 已提交。
    /// </summary>
    Committed = 2,

    /// <summary>
    /// 已回滚。
    /// </summary>
    RolledBack = 3,
}
