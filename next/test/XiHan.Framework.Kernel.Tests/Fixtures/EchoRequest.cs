// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 回声操作的请求：文本与重复次数。实现自校验：文本为 <c>invalid</c> 时报一条针对 <c>Text</c> 的结果，Validate 阶段据此拒绝。
/// </summary>
/// <param name="Text">要回声的文本；为空时操作返回校验失败。</param>
/// <param name="Repeat">重复次数。</param>
internal sealed record EchoRequest(string? Text, int Repeat = 1) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Text == "invalid")
        {
            yield return new ValidationResult("The text is not allowed.", [nameof(Text)]);
        }
    }
}
