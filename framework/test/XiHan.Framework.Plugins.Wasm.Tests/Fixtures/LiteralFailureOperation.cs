// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 宿主侧的操作：总以文案里有孤立代理项的自由文本错误失败
/// </summary>
/// <remarks>
/// 外层与内部错误的文案都是 <see cref="Text"/>，细节是合法的；用来核对宿主把它写给插件时文案里编不成 UTF-8 的部分换成 U+FFFD（ADR-0339）
/// </remarks>
internal sealed class LiteralFailureOperation : IOperation<PingRequest, PingResponse>
{
    /// <summary>
    /// 外层错误的错误码
    /// </summary>
    public const string Code = "ORDERS_UNEXPECTED_001";

    /// <summary>
    /// 内部错误的错误码
    /// </summary>
    public const string InnerCode = "ORDERS_UNEXPECTED_002";

    /// <summary>
    /// 错误的文案：孤立的高、低代理项各一个，另有一对成对的代理项
    /// </summary>
    public const string Text = "lone \uD800 high, \uDC00 low, pair 😀";

    /// <summary>
    /// 换成 U+FFFD 之后的文案
    /// </summary>
    public const string ReplacedText = "lone � high, � low, pair 😀";

    /// <inheritdoc />
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Failure<PingResponse>(Error.Literal(Code, Text, MessageArguments.Of(("order", request.Name)), Error.Literal(InnerCode, Text))));
}
