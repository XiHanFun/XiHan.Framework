// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 诊断文本里的机密遮蔽。
/// </summary>
/// <remarks>
/// 选项与携带凭据的记录在 <c>ToString()</c>（记录的 <c>PrintMembers</c>）里以它打印口令、密钥、令牌与整串连接串，防止它们随对象进入日志、异常消息或调试输出（RFC-0014「选项里的凭据」）。用户名、访问密钥 ID 与端点不是机密，照常打印。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class Redaction
{
    /// <summary>
    /// 替换机密的文本。
    /// </summary>
    /// <remarks>
    /// 与持久化的 <c>ConnectionStrings.Mask</c>、配置的 <c>SecretValue.Redacted</c> 相同。
    /// </remarks>
    public const string Mask = "***";

    /// <summary>
    /// 遮蔽一个机密值。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 原样返回（记录打印为空，表示没有配置）；其余一律返回 <see cref="Mask"/>，空串也不例外，不透露长度与内容。
    /// </remarks>
    /// <param name="value">机密值。</param>
    public static string? Redact(string? value) => value is null ? null : Mask;
}
