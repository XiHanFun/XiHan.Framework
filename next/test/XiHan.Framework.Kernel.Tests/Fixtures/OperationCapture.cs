// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 记录管道里看到的操作 ID，验证描述在管道开始前已进入上下文。
/// </summary>
internal sealed class OperationCapture
{
    /// <summary>
    /// Authorize 阶段看到的操作 ID。
    /// </summary>
    /// <remarks>
    /// 不是操作调用时记 <c>-</c>。
    /// </remarks>
    public List<string> Seen { get; } = [];
}
