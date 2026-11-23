// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Secrets;

/// <summary>
/// 密钥库（RFC-0014）
/// </summary>
/// <remarks>
/// 按名字批量读取，只读；写入与轮换属于运维工具，不在应用契约里
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface ISecretStore
{
    /// <summary>
    /// 批量读取，逐项结果与 <paramref name="names"/> 同序、等长，重复项各自作答，空集合返回空
    /// </summary>
    /// <remarks>
    /// 不存在 → <see cref="ConfigurationErrors.SecretNotFound"/>；无权限 → <see cref="ConfigurationErrors.SecretDenied"/>；不可达 → <see cref="ConfigurationErrors.SecretsUnavailable"/>
    /// </remarks>
    ValueTask<IReadOnlyList<Result<SecretValue>>> GetAsync(IReadOnlyList<SecretName> names, CancellationToken cancellationToken);
}
