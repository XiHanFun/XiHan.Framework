using XiHan.Framework.Shared.Results;

namespace XiHan.Framework.Validation.Abstractions;

/// <summary>
/// 表示对象验证器抽象。
/// </summary>
public interface IObjectValidator
{
    /// <summary>
    /// 验证指定对象。
    /// </summary>
    /// <param name="instance">待验证对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>验证结果。</returns>
    Task<OperationResult> ValidateAsync(object instance, CancellationToken cancellationToken = default);
}
