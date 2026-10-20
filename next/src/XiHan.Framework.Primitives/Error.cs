// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 可恢复失败的描述。<see cref="Code"/> 面向程序，<see cref="Message"/> 面向人；客户端只应依赖错误码。
/// 线上编码：<c>{"code":"…","message":"…","details":{…},"inner":{…}}</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Error 是 XiHan Spec 的规范术语，与 Go error、Rust Err 对齐；框架不面向 Visual Basic 使用者。")]
public sealed record Error
{
    /// <summary>
    /// 创建错误。
    /// </summary>
    /// <param name="code">符合 <see cref="ErrorCode"/> 格式的错误码。</param>
    /// <param name="message">面向人的说明，不能为空白。</param>
    /// <param name="details">可选的结构化细节，只允许 JSON 可表示的值。</param>
    /// <param name="inner">可选的内部错误。</param>
    public Error(string code, string message, IReadOnlyDictionary<string, object?>? details = null, Error? inner = null)
    {
        Code = ErrorCode.Validate(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Details = details;
        Inner = inner;
    }

    /// <summary>
    /// 错误码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 面向人的说明。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 结构化细节；按引用比较。
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Details { get; }

    /// <summary>
    /// 内部错误。
    /// </summary>
    public Error? Inner { get; }

    /// <summary>
    /// 错误码的 KIND 段，例如 <c>CONFLICT</c>。协议适配器据此映射状态码。
    /// </summary>
    public string Kind => ErrorCode.KindOf(Code);

    /// <summary>
    /// 创建校验错误；错误码的 KIND 段必须是 <c>VALIDATION</c>。
    /// </summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "VALIDATION"), message, details);

    /// <summary>
    /// 创建未找到错误；错误码的 KIND 段必须是 <c>NOTFOUND</c>。
    /// </summary>
    public static Error NotFound(string code, string message) => new(RequireKind(code, "NOTFOUND"), message);

    /// <summary>
    /// 创建冲突错误；错误码的 KIND 段必须是 <c>CONFLICT</c>。
    /// </summary>
    public static Error Conflict(string code, string message) => new(RequireKind(code, "CONFLICT"), message);

    /// <summary>
    /// 创建拒绝错误；错误码的 KIND 段必须是 <c>DENIED</c>。
    /// </summary>
    public static Error Denied(string code, string message) => new(RequireKind(code, "DENIED"), message);

    /// <summary>
    /// 创建不可用错误；错误码的 KIND 段必须是 <c>UNAVAILABLE</c>。
    /// </summary>
    public static Error Unavailable(string code, string message) => new(RequireKind(code, "UNAVAILABLE"), message);

    /// <summary>
    /// 创建未预期错误；错误码的 KIND 段必须是 <c>UNEXPECTED</c>。
    /// </summary>
    public static Error Unexpected(string code, string message, Error? inner = null) => new(RequireKind(code, "UNEXPECTED"), message, null, inner);

    /// <inheritdoc />
    public override string ToString() => $"[{Code}] {Message}";

    private static string RequireKind(string code, string kind)
    {
        if (!string.Equals(ErrorCode.KindOf(code), kind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"错误码 '{code}' 的 KIND 段必须是 {kind}。", nameof(code));
        }

        return code;
    }
}
