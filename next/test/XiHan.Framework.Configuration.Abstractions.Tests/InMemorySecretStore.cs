// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 进程内的密钥库：每次写入换版本；可让某个名字返回拒绝，或整体抛出。
/// </summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 名字 → 值。
    /// </summary>
    private readonly Dictionary<string, SecretValue> _secrets = new(StringComparer.Ordinal);

    /// <summary>
    /// 递增的版本号。
    /// </summary>
    private int _version;

    /// <summary>
    /// 读取次数。
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// 返回拒绝的名字。
    /// </summary>
    public HashSet<string> Denied { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 为真时每次读取都抛出。
    /// </summary>
    public bool Throw { get; set; }

    /// <summary>
    /// 写入。
    /// </summary>
    public void Put(SecretName name, string value)
    {
        lock (_lock)
        {
            _version++;
            _secrets[name.Value] = new SecretValue(value, _version.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SecretLookup>> GetAsync(IReadOnlyList<SecretName> names, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Reads++;
            if (Throw)
            {
                throw new InvalidOperationException("The secret store is broken.");
            }

            var lookups = new List<SecretLookup>(names.Count);
            foreach (var name in names)
            {
                Result<SecretValue> result = Denied.Contains(name.Value)
                    ? Error.Denied(ConfigurationErrors.SecretDenied, ConfigurationMessages.SecretDenied, MessageArguments.Of(("name", name.Value)))
                    : _secrets.TryGetValue(name.Value, out var secret)
                        ? Result.Success(secret)
                        : Error.NotFound(ConfigurationErrors.SecretNotFound, ConfigurationMessages.SecretNotFound, MessageArguments.Of(("name", name.Value)));
                lookups.Add(new SecretLookup(name, result));
            }

            return ValueTask.FromResult<IReadOnlyList<SecretLookup>>(lookups);
        }
    }
}
