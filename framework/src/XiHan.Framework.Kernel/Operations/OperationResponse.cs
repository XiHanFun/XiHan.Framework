// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// XGI 响应消息
/// </summary>
/// <remarks>
/// 调用结果与成功时的响应载荷（响应类型的 UTF-8 JSON）；失败时载荷为空，错误在 <see cref="Outcome"/> 里，由协议适配器翻译成各自的错误信封。成员经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OperationResponse
{
    /// <summary>
    /// 调用结果
    /// </summary>
    public required Result Outcome { get; init; }

    /// <summary>
    /// 成功时的响应载荷
    /// </summary>
    /// <remarks>
    /// 失败时为空
    /// </remarks>
    public ReadOnlyMemory<byte> Payload { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Outcome.IsSuccess;

    /// <summary>
    /// 失败时的错误
    /// </summary>
    /// <remarks>
    /// 成功为 <see langword="null"/>
    /// </remarks>
    public Error? Error => Outcome.Error;

    /// <summary>
    /// 由失败结果创建响应（载荷为空）
    /// </summary>
    public static OperationResponse Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new() { Outcome = Result.Failure(error) };
    }
}
