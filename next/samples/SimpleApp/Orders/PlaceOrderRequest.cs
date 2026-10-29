// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

/// <summary>
/// <c>POST /orders</c> 的请求体。
/// </summary>
/// <remarks>
/// DataAnnotations 由 <c>Kernel.Generators</c> 折进请求 Schema（名称 1 到 64 个字符、金额 1 到 1000000），Validate 阶段按 Schema 拒绝（<c>KERNEL_VALIDATION_003</c>）；自校验（名称不能只有空白）在解码后执行（<c>KERNEL_VALIDATION_004</c>），文案按消息键取。两种拒绝的 Problem Details 都带字段级 <c>errors</c>（如 <c>{"errors":{"name":["长度必须 >= 1"],"amount":["值必须 >= 1"]}}</c>，按 <c>Accept-Language</c> 翻译），前端表单据此定位字段。
/// </remarks>
/// <param name="Name">订单名称。</param>
/// <param name="Amount">订单金额。</param>
public sealed record PlaceOrderRequest([Required, StringLength(64, MinimumLength = 1)] string Name, [Range(1, 1_000_000)] int Amount) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Schema 只能约束长度，不能表达"全是空白"；这类规则放在自校验里，文案走消息键按调用方语言取
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult(SampleMessages.OrderNameBlank.L(), [nameof(Name)]);
        }
    }
}
