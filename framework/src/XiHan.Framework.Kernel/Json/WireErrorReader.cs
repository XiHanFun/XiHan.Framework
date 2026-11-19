// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// 从线上编码还原 Error
/// </summary>
/// <remarks>
/// RFC-0001「Result」的 Error 规则（ADR-0244）：<c>code</c> 必填且合文法；<c>message</c> 必填、是非空字符串；<c>messageKey</c> 有就必须合文法；<c>details</c> 必须是 JSON 对象；<c>inner</c> 必须是错误对象；可选成员写成 <c>null</c> 不合法，未知成员忽略。
/// 读出的消息键与文案来自线上，不是本包的常量，所以这里是架构测试允许线上还原的文件之一；不合规则时的错误由 <see cref="ResultJson"/> 构造
/// </remarks>
internal static class WireErrorReader
{
    /// <summary>
    /// 读一个错误对象，内部错误递归
    /// </summary>
    /// <remarks>
    /// 调用方已过共同的 JSON 读取检查：没有重复成员，嵌套不超过 64 层
    /// </remarks>
    /// <param name="element">错误对象</param>
    /// <param name="path">它的路径，用于报出不合规则的成员</param>
    public static Result<Error> Read(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result.Failure<Error>(ResultJson.Invalid(path));
        }

        if (StringMember(element, "code") is not { } code || !ErrorCode.IsValid(code))
        {
            return Result.Failure<Error>(ResultJson.Invalid(JsonPath.Member(path, "code")));
        }

        // 规范只要求非空；只有空白的文案 Error 表示不了（Literal 与 WithText 都拒绝），不悄悄换成别的文案
        if (StringMember(element, "message") is not { } message || string.IsNullOrWhiteSpace(message))
        {
            return Result.Failure<Error>(ResultJson.Invalid(JsonPath.Member(path, "message")));
        }

        string? key = null;
        if (element.TryGetProperty("messageKey", out var keyMember))
        {
            // 可选成员写成 null 不合法，与缺失不同
            key = keyMember.ValueKind == JsonValueKind.String ? keyMember.GetString() : null;
            if (!MessageTemplate.IsValidKey(key))
            {
                return Result.Failure<Error>(ResultJson.Invalid(JsonPath.Member(path, "messageKey")));
            }
        }

        IReadOnlyDictionary<string, object?>? details = null;
        if (element.TryGetProperty("details", out var detailsMember))
        {
            if (detailsMember.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<Error>(ResultJson.Invalid(JsonPath.Member(path, "details")));
            }

            // 空细节与没有细节相同，再写出时不写
            if (detailsMember.EnumerateObject().Any())
            {
                details = JsonValues.ReadObject(detailsMember);
            }
        }

        Error? inner = null;
        if (element.TryGetProperty("inner", out var innerMember))
        {
            var nested = Read(innerMember, JsonPath.Member(path, "inner"));
            if (nested.Error is { } malformed)
            {
                return Result.Failure<Error>(malformed);
            }

            inner = nested.ValueOrThrow();
        }

        // 带键的错误保留键让接收端可再翻译，文案取发送端已解析的 message；没有键的是自由文本
        var read = key is null ? Error.Literal(code, message, details, inner) : new Error(code, key, details, inner).WithText(message);
        return Result.Success(read);
    }

    /// <summary>
    /// 取对象的字符串成员
    /// </summary>
    /// <returns>成员没有或不是字符串（含 <c>null</c>）时为 <see langword="null"/></returns>
    private static string? StringMember(JsonElement element, string name)
        => element.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String ? member.GetString() : null;
}
