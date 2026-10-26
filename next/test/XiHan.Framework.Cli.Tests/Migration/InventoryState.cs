// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// <see cref="InventoryFixer"/> 在第一遍收集的跨文件事实。
/// </summary>
internal sealed class InventoryState
{
    /// <summary>
    /// 原文里用到 <c>LegacyClock</c> 的文件（相对路径）。
    /// </summary>
    public List<string> Files { get; } = [];
}
