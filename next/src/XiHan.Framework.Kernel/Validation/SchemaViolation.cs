// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Validation;

/// <summary>
/// 一条校验违规：载荷里的位置（<c>$</c> 根、<c>$.name</c>、<c>$.lines[0].sku</c>）与原因（关键字级的英文诊断，如 <c>expected type 'integer'</c>）。
/// 进入失败结果的 <c>violations</c> 细节时按 <see cref="ToString"/> 的 <c>路径: 原因</c> 形式列出。
/// </summary>
/// <param name="Path">违规位置。</param>
/// <param name="Reason">原因。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SchemaViolation(string Path, string Reason)
{
    /// <summary>
    /// 违规位置。
    /// </summary>
    public string Path { get; } = !string.IsNullOrWhiteSpace(Path) ? Path : throw new ArgumentException("The violation path must not be empty.", nameof(Path));

    /// <summary>
    /// 原因。
    /// </summary>
    public string Reason { get; } = !string.IsNullOrWhiteSpace(Reason) ? Reason : throw new ArgumentException("The violation reason must not be empty.", nameof(Reason));

    /// <inheritdoc />
    public override string ToString() => $"{Path}: {Reason}";
}
