using XiHan.Framework.Shared.Results;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.Validation;

/// <summary>
/// 表示默认的空实现对象验证器。
/// </summary>
/// <remarks>
/// 当前阶段用于建立统一扩展点，后续再接入成熟验证实现。
/// </remarks>
public sealed class NoopObjectValidator : IObjectValidator
{
    /// <inheritdoc />
    public Task<OperationResult> ValidateAsync(object instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Task.FromResult(OperationResult.Success());
    }
}
