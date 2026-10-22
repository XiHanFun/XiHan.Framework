// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// 从操作 ID 推导 tool 名（<c>spec/conventions/operation-ids.md</c>）：<c>{resource}_{verb}</c>，如 <c>orders.create</c> → <c>orders_create</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpToolNameConvention
{
    /// <summary>
    /// 推导 tool 名；结果可能超过 <see cref="McpToolName.MaxLength"/>，由调用方校验并用配置覆盖。
    /// </summary>
    public static string Derive(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Resource + "_" + contract.Verb;
    }
}
