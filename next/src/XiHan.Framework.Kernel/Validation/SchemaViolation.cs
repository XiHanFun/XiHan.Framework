// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Validation;

/// <summary>
/// 一条校验违规：载荷里的位置（<c>$</c> 根、<c>$.name</c>、<c>$.lines[0].sku</c>）、原因（关键字级的英文诊断，如 <c>expected type 'integer'</c>），
/// 以及可选的消息键与参数（校验器给出 <c>kernel.validation.*</c>，宿主据此按调用方区域翻译成字段级错误；请求对象自校验的结果没有键，原因就是应用给的文案）。
/// 进入失败结果的 <c>violations</c> 细节时按 <see cref="ToString"/> 的 <c>路径: 原因</c> 形式列出，<c>errors</c> 细节保留本对象。
/// </summary>
/// <param name="Path">违规位置。</param>
/// <param name="Reason">原因。</param>
/// <param name="MessageKey">消息键；没有时宿主直接用 <paramref name="Reason"/>。</param>
/// <param name="Arguments">消息参数。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SchemaViolation(string Path, string Reason, string? MessageKey = null, IReadOnlyDictionary<string, object?>? Arguments = null)
{
    /// <summary>
    /// 违规位置。
    /// </summary>
    public string Path { get; } = !string.IsNullOrWhiteSpace(Path) ? Path : throw new ArgumentException("The violation path must not be empty.", nameof(Path));

    /// <summary>
    /// 原因。
    /// </summary>
    public string Reason { get; } = !string.IsNullOrWhiteSpace(Reason) ? Reason : throw new ArgumentException("The violation reason must not be empty.", nameof(Reason));

    /// <summary>
    /// 字段键：去掉根 <c>$</c> 与开头的点的路径（<c>$</c> → 空串、<c>$.name</c> → <c>name</c>、<c>$.lines[0].sku</c> → <c>lines[0].sku</c>），即 Problem Details <c>errors</c> 的键。
    /// </summary>
    public string Field => Path switch
    {
        "$" => string.Empty,
        _ when Path.StartsWith("$.", StringComparison.Ordinal) => Path[2..],
        _ when Path.StartsWith('$') => Path[1..],
        _ => Path,
    };

    /// <inheritdoc />
    public override string ToString() => $"{Path}: {Reason}";
}
