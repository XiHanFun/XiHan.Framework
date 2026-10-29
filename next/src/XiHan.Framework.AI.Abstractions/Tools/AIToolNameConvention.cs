// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tools;

/// <summary>
/// 操作工具名的默认推导。
/// </summary>
/// <remarks>
/// <c>&lt;resource&gt;_&lt;verb&gt;</c>，<c>-</c> 与 <c>.</c> 换成 <c>_</c>（模型 API 的工具名只允许字母、数字、下划线与连字符）；超过 64 个字符时截成前 55 个字符加 <c>_</c> 与全名 SHA-256 的前 8 位十六进制，保证任何操作 ID 都推得出合法且稳定的名字。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class AIToolNameConvention
{
    /// <summary>
    /// 工具名的最大长度（模型 API 的限制）。
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 截断后保留的前缀长度：加上 <c>_</c> 与 8 位哈希正好 64。
    /// </summary>
    private const int PrefixLength = MaxLength - 9;

    /// <summary>
    /// 推导。
    /// </summary>
    public static string Derive(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var name = (contract.Resource + "_" + contract.Verb).Replace('-', '_').Replace('.', '_');
        return name.Length <= MaxLength ? name : Shorten(name);
    }

    /// <summary>
    /// 操作描述的消息键。
    /// </summary>
    /// <remarks>
    /// <c>&lt;操作 ID&gt;.description</c>（ID 里的 <c>-</c> 换成 <c>_</c>），与 MCP 的 tool 描述同一个键。
    /// </remarks>
    public static string DescriptionKeyOf(string operationId)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        return operationId.Replace('-', '_') + ".description";
    }

    /// <summary>
    /// 过长的名字。
    /// </summary>
    /// <remarks>
    /// 前缀 + <c>_</c> + 全名哈希的前 8 位；哈希取自全名，前缀相同的不同操作不会撞名。
    /// </remarks>
    private static string Shorten(string name)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
        return name[..PrefixLength].TrimEnd('_') + "_" + hash;
    }
}
