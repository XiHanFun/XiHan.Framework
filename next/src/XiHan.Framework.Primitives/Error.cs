// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 可恢复失败的描述。
/// </summary>
/// <remarks>
/// <see cref="Code"/> 面向程序，<see cref="MessageKey"/> 与 <see cref="Details"/> 决定面向人的文案；客户端只应依赖错误码。文案不写在代码里：本地化器按消息键与调用方区域从语言文件解析（宿主在 Egress 自动做，任何地方可用 <c>key.L()</c>）。没有本地化器时 <see cref="Message"/> 是键与参数的描述；来自线上或异常的自由文本用 <see cref="Literal"/> 创建。线上编码：<c>{"code":"…","message":"…","messageKey":"…","details":{…},"inner":{…}}</c>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Error 是 XiHan Spec 的规范术语，与 Go error、Rust Err 对齐；框架不面向 Visual Basic 使用者。")]
public sealed record Error
{
    /// <summary>
    /// 用消息键创建错误。
    /// </summary>
    /// <param name="code">符合 <see cref="ErrorCode"/> 格式的错误码。</param>
    /// <param name="messageKey">符合 <see cref="MessageTemplate.IsValidKey"/> 文法的消息键，来自所属包的 <c>&lt;领域&gt;Messages</c> 常量。</param>
    /// <param name="details">模板参数，同时作为结构化细节；只允许 JSON 可表示的值。</param>
    /// <param name="inner">可选的内部错误。</param>
    public Error(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null, Error? inner = null)
        : this(ErrorCode.Validate(code), MessageTemplate.ValidateKey(messageKey, nameof(messageKey)), null, details, inner)
    {
    }

    /// <summary>
    /// 全字段构造，供公开入口与 <c>With*</c> 复制方法使用。
    /// </summary>
    /// <remarks>
    /// 调用方已完成校验。
    /// </remarks>
    private Error(string code, string? messageKey, string? text, IReadOnlyDictionary<string, object?>? details, Error? inner)
    {
        Code = code;
        MessageKey = messageKey;
        Text = text;
        Details = details;
        Inner = inner;
    }

    /// <summary>
    /// 错误码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 消息键。
    /// </summary>
    /// <remarks>
    /// 用 <see cref="Literal"/> 创建的自由文本错误为 <see langword="null"/>。
    /// </remarks>
    public string? MessageKey { get; }

    /// <summary>
    /// 已解析的文案。
    /// </summary>
    /// <remarks>
    /// 自由文本错误的原文，或本地化器用 <see cref="WithText"/> 填入的译文；未解析时为 <see langword="null"/>。
    /// </remarks>
    public string? Text { get; }

    /// <summary>
    /// 模板参数，也是结构化细节。
    /// </summary>
    /// <remarks>
    /// 按引用比较。
    /// </remarks>
    public IReadOnlyDictionary<string, object?>? Details { get; }

    /// <summary>
    /// 内部错误。
    /// </summary>
    public Error? Inner { get; }

    /// <summary>
    /// 面向人的说明。
    /// </summary>
    /// <remarks>
    /// 已解析的 <see cref="Text"/>，否则按进程默认区域（<see cref="MessageScope.Default"/>）解析消息键；没有默认作用域时是键与参数的描述。调用方语言的文案由宿主在 Egress 用本地化器另行解析，不影响日志里的默认语言。
    /// </remarks>
    public string Message => Text ?? (MessageScope.Default is { } scope ? scope.Resolve(MessageKey!, Details) : MessageTemplate.Describe(MessageKey!, Details));

    /// <summary>
    /// 错误码的 KIND 段，例如 <c>CONFLICT</c>。
    /// </summary>
    /// <remarks>
    /// 协议适配器据此映射状态码。
    /// </remarks>
    public string Kind => ErrorCode.KindOf(Code);

    /// <summary>
    /// 用自由文本创建错误，不参与本地化。
    /// </summary>
    /// <remarks>
    /// 用于线上编码的还原与异常转换。
    /// </remarks>
    /// <param name="code">符合 <see cref="ErrorCode"/> 格式的错误码。</param>
    /// <param name="text">面向人的说明，不能为空白。</param>
    /// <param name="details">可选的结构化细节，只允许 JSON 可表示的值。</param>
    /// <param name="inner">可选的内部错误。</param>
    public static Error Literal(string code, string text, IReadOnlyDictionary<string, object?>? details = null, Error? inner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new(ErrorCode.Validate(code), null, text, details, inner);
    }

    /// <summary>
    /// 创建校验错误。
    /// </summary>
    /// <remarks>
    /// 错误码的 KIND 段必须是 <c>VALIDATION</c>。
    /// </remarks>
    public static Error Validation(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "VALIDATION"), messageKey, details);

    /// <summary>
    /// 创建未找到错误。
    /// </summary>
    /// <remarks>
    /// 错误码的 KIND 段必须是 <c>NOTFOUND</c>。
    /// </remarks>
    public static Error NotFound(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "NOTFOUND"), messageKey, details);

    /// <summary>
    /// 创建冲突错误。
    /// </summary>
    /// <remarks>
    /// 错误码的 KIND 段必须是 <c>CONFLICT</c>。
    /// </remarks>
    public static Error Conflict(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "CONFLICT"), messageKey, details);

    /// <summary>
    /// 创建拒绝错误。
    /// </summary>
    /// <remarks>
    /// 错误码的 KIND 段必须是 <c>DENIED</c>。
    /// </remarks>
    public static Error Denied(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "DENIED"), messageKey, details);

    /// <summary>
    /// 创建不可用错误。
    /// </summary>
    /// <remarks>
    /// 错误码的 KIND 段必须是 <c>UNAVAILABLE</c>。
    /// </remarks>
    public static Error Unavailable(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "UNAVAILABLE"), messageKey, details);

    /// <summary>
    /// 创建未预期错误。
    /// </summary>
    /// <remarks>
    /// 错误码的 KIND 段必须是 <c>UNEXPECTED</c>。
    /// </remarks>
    public static Error Unexpected(string code, string messageKey, Error? inner = null, IReadOnlyDictionary<string, object?>? details = null)
        => new(RequireKind(code, "UNEXPECTED"), messageKey, details, inner);

    /// <summary>
    /// 复制一份并换成另一个错误码，消息键、文案、细节与内部错误保留。
    /// </summary>
    /// <remarks>
    /// 用于把下层错误归入本层错误码而不丢失译文。
    /// </remarks>
    public Error WithCode(string code) => new(ErrorCode.Validate(code), MessageKey, Text, Details, Inner);

    /// <summary>
    /// 复制一份并填入已解析的文案。
    /// </summary>
    /// <remarks>
    /// 本地化器按调用方区域解析后调用。
    /// </remarks>
    public Error WithText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new(Code, MessageKey, text, Details, Inner);
    }

    /// <inheritdoc />
    public override string ToString() => $"[{Code}] {Message}";

    /// <summary>
    /// 校验错误码的 KIND 段等于 <paramref name="kind"/>，供各工厂方法在构造前拦截种类不符的错误码。
    /// </summary>
    /// <remarks>
    /// 通过时原样返回错误码。
    /// </remarks>
    private static string RequireKind(string code, string kind)
    {
        if (!string.Equals(ErrorCode.KindOf(code), kind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The KIND segment of error code '{code}' must be '{kind}'.", nameof(code));
        }

        return code;
    }
}
