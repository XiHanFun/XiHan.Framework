// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 中间件在所属阶段内的相对位置。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum Placement
{
    /// <summary>阶段末尾（默认）。</summary>
    End = 0,

    /// <summary>阶段开头。</summary>
    Beginning = 1,
}
