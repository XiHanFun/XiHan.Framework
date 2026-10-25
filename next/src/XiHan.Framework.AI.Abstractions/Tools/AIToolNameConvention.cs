// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tools;

/// <summary>
/// 操作工具名的默认推导：<c>&lt;resource&gt;_&lt;verb&gt;</c>，<c>-</c> 与 <c>.</c> 换成 <c>_</c>（模型 API 的工具名只允许字母、数字、下划线与连字符）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AIToolNameConvention
{
    /// <summary>
    /// 推导。
    /// </summary>
    public static string Derive(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return (contract.Resource + "_" + contract.Verb).Replace('-', '_').Replace('.', '_');
    }

    /// <summary>
    /// 操作描述的消息键：<c>&lt;操作 ID&gt;.description</c>（ID 里的 <c>-</c> 换成 <c>_</c>），与 MCP 的 tool 描述同一个键。
    /// </summary>
    public static string DescriptionKeyOf(string operationId)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        return operationId.Replace('-', '_') + ".description";
    }
}
