// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Conformance;

/// <summary>
/// 密钥库套件的夹具：被测密钥库与写入密钥的钩子。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class SecretStoreFixture
{
    /// <summary>
    /// 被测密钥库。
    /// </summary>
    public abstract ISecretStore Store { get; }

    /// <summary>
    /// 写入（创建或覆盖）一个密钥；返回后密钥库应能读到。
    /// </summary>
    public abstract Task PutAsync(SecretName name, string value, CancellationToken cancellationToken);
}
