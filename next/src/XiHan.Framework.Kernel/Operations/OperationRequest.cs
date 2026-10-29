// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// XGI 请求消息。
/// </summary>
/// <remarks>
/// 协议适配器把线上请求翻译成它交给 <see cref="IOperationInvoker"/>。载荷是请求类型的 UTF-8 JSON；空载荷等价于 <c>{}</c>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OperationRequest
{
    /// <summary>
    /// 创建请求消息。
    /// </summary>
    /// <param name="id">符合 <see cref="OperationId"/> 文法的操作 ID。</param>
    /// <param name="payload">请求类型的 UTF-8 JSON；空表示 <c>{}</c>。</param>
    public OperationRequest(string id, ReadOnlyMemory<byte> payload = default)
    {
        Id = OperationId.Validate(id);
        Payload = payload;
    }

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 请求载荷（UTF-8 JSON）。
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }
}
