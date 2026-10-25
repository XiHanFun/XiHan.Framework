// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 波次队列里一项工作的种类。
/// </summary>
internal enum WorkItemKind
{
    /// <summary>新执行一个节点。</summary>
    Fresh = 0,

    /// <summary>从书签恢复节点。</summary>
    Resume = 1,

    /// <summary>重试故障节点。</summary>
    Retry = 2,
}
