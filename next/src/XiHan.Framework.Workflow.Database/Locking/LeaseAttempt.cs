// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Locking;

/// <summary>
/// 一次租约获取尝试的结果。
/// </summary>
internal enum LeaseAttempt
{
    /// <summary>
    /// 拿到了：新行已插入或过期的行已接管。
    /// </summary>
    Acquired = 1,

    /// <summary>
    /// 别人持有且未过期：等一会再试。
    /// </summary>
    Held = 2,

    /// <summary>
    /// 与别的获取者撞上（插入时别人先插入、接管时别人先改）：立即重试。
    /// </summary>
    Contended = 3,
}
