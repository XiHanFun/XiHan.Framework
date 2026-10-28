// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp.Tools;

/// <summary>
/// tool 的行为提示（MCP <c>annotations</c>：<c>readOnlyHint</c>、<c>destructiveHint</c>、<c>idempotentHint</c>），按操作动词与契约的只读声明推导。
/// </summary>
/// <param name="ReadOnly">只读，不改变状态。</param>
/// <param name="Destructive">可能破坏性地修改状态。</param>
/// <param name="Idempotent">重复调用同样的参数没有额外效果。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpToolAnnotations(bool ReadOnly, bool Destructive, bool Idempotent)
{
    /// <summary>
    /// 推导：只读契约（<see cref="OperationContract.IsReadOnly"/>：<c>get</c> / <c>list</c>，或声明了只读的非标准动词如 <c>tree</c>）只读且幂等；<c>update</c> 幂等；
    /// <c>delete</c> 破坏性且幂等；<c>create</c> 既不破坏也不幂等；其他动词按 MCP 默认值（可能破坏、不幂等）。
    /// </summary>
    public static McpToolAnnotations For(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Verb switch
        {
            "update" => new McpToolAnnotations(ReadOnly: false, Destructive: false, Idempotent: true),
            "delete" => new McpToolAnnotations(ReadOnly: false, Destructive: true, Idempotent: true),
            "create" => new McpToolAnnotations(ReadOnly: false, Destructive: false, Idempotent: false),
            _ when contract.IsReadOnly => new McpToolAnnotations(ReadOnly: true, Destructive: false, Idempotent: true),
            _ => new McpToolAnnotations(ReadOnly: false, Destructive: true, Idempotent: false),
        };
    }
}
