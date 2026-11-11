// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp.Tools;

/// <summary>
/// tool 的行为提示
/// </summary>
/// <remarks>
/// MCP <c>annotations</c>：<c>readOnlyHint</c>、<c>destructiveHint</c>、<c>idempotentHint</c>。按操作动词与契约的只读声明推导。成员经初始化器给出、缺省为假，以后补 MCP 规范的其他提示只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpToolAnnotations
{
    /// <summary>
    /// 只读，不改变状态
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 可能破坏性地修改状态
    /// </summary>
    public bool Destructive { get; init; }

    /// <summary>
    /// 重复调用同样的参数没有额外效果
    /// </summary>
    public bool Idempotent { get; init; }

    /// <summary>
    /// 推导
    /// </summary>
    /// <remarks>
    /// 只读契约（<see cref="OperationContract.IsReadOnly"/>：<c>get</c> / <c>list</c>，或声明了只读的非标准动词如 <c>tree</c>）只读且幂等；<c>update</c> 幂等；<c>delete</c> 破坏性且幂等；<c>create</c> 既不破坏也不幂等；其他动词按 MCP 默认值（可能破坏、不幂等）
    /// </remarks>
    public static McpToolAnnotations For(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Verb switch
        {
            "update" => new McpToolAnnotations { Idempotent = true },
            "delete" => new McpToolAnnotations { Destructive = true, Idempotent = true },
            "create" => new McpToolAnnotations(),
            _ when contract.IsReadOnly => new McpToolAnnotations { ReadOnly = true, Idempotent = true },
            _ => new McpToolAnnotations { Destructive = true },
        };
    }
}
