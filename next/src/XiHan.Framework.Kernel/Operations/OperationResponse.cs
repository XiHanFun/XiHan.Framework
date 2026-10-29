// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// XGI 响应消息。
/// </summary>
/// <remarks>
/// 调用结果与成功时的响应载荷（响应类型的 UTF-8 JSON）；失败时载荷为空，错误在 <see cref="Outcome"/> 里，由协议适配器翻译成各自的错误信封。
/// </remarks>
/// <param name="Outcome">调用结果。</param>
/// <param name="Payload">成功时的响应载荷；失败时为空。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OperationResponse(Result Outcome, ReadOnlyMemory<byte> Payload)
{
    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool IsSuccess => Outcome.IsSuccess;

    /// <summary>
    /// 失败时的错误。
    /// </summary>
    /// <remarks>
    /// 成功为 <see langword="null"/>。
    /// </remarks>
    public Error? Error => Outcome.Error;

    /// <summary>
    /// 由失败结果创建响应（载荷为空）。
    /// </summary>
    public static OperationResponse Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(Result.Failure(error), ReadOnlyMemory<byte>.Empty);
    }
}
