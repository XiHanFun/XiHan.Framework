// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Validation;

/// <summary>
/// 一条校验违规
/// </summary>
/// <remarks>
/// 载荷里的位置（<c>$</c> 根、<c>$.name</c>、<c>$.lines[0].sku</c>）、原因（关键字级的英文诊断，如 <c>expected type 'integer'</c>），以及可选的消息键与参数（校验器给出 <c>kernel.validation.*</c>，宿主据此按调用方区域翻译成字段级错误；请求对象自校验的结果没有键，原因就是应用给的文案）。进入失败结果的 <c>violations</c> 细节时按 <see cref="ToString"/> 的 <c>路径: 原因</c> 形式列出，<c>errors</c> 细节保留本对象。
/// 位置与原因经构造给出，消息键与参数经初始化器给出，以后加成员只加属性（RFC-0001「数据类型的形状」）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record SchemaViolation
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="path">违规位置，不能为空白</param>
    /// <param name="reason">原因，不能为空白</param>
    /// <exception cref="ArgumentException">位置或原因为空白</exception>
    public SchemaViolation(string path, string reason)
    {
        Path = !string.IsNullOrWhiteSpace(path) ? path : throw new ArgumentException("The violation path must not be empty.", nameof(path));
        Reason = !string.IsNullOrWhiteSpace(reason) ? reason : throw new ArgumentException("The violation reason must not be empty.", nameof(reason));
    }

    /// <summary>
    /// 违规位置
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 原因
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// 消息键
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时宿主直接用 <see cref="Reason"/>
    /// </remarks>
    public string? MessageKey { get; init; }

    /// <summary>
    /// 消息参数
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }

    /// <summary>
    /// 字段键
    /// </summary>
    /// <remarks>
    /// 去掉根 <c>$</c> 与开头的点的路径（<c>$</c> → 空串、<c>$.name</c> → <c>name</c>、<c>$.lines[0].sku</c> → <c>lines[0].sku</c>），即 Problem Details <c>errors</c> 的键
    /// </remarks>
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
