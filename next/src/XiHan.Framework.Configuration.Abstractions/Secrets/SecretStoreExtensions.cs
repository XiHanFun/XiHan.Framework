// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Secrets;

/// <summary>
/// 单条便捷方法。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SecretStoreExtensions
{
    /// <summary>
    /// 读取一个密钥。
    /// </summary>
    public static async ValueTask<Result<SecretValue>> GetAsync(this ISecretStore store, SecretName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var lookups = await store.GetAsync([name], cancellationToken).ConfigureAwait(false);
        return lookups[0].Result;
    }
}
