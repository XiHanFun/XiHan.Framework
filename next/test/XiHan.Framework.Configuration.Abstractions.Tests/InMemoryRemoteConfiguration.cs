// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 进程内的远程配置：发布即换版本；可让取值失败或抛出。
/// </summary>
internal sealed class InMemoryRemoteConfiguration : IRemoteConfigurationClient
{
    /// <summary>
    /// 保护文档表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 命名空间 → 文档。
    /// </summary>
    private readonly Dictionary<string, ConfigurationDocument> _documents = new(StringComparer.Ordinal);

    /// <summary>
    /// 递增的版本号。
    /// </summary>
    private int _version;

    /// <summary>
    /// 取值次数。
    /// </summary>
    public int Fetches { get; private set; }

    /// <summary>
    /// 非空时每次取值都返回这个失败。
    /// </summary>
    public Error? Fail { get; set; }

    /// <summary>
    /// 为真时每次取值都抛出。
    /// </summary>
    public bool Throw { get; set; }

    /// <summary>
    /// 发布（创建或整体替换）。
    /// </summary>
    public void Publish(string namespaceName, IReadOnlyDictionary<string, string?> values)
    {
        lock (_lock)
        {
            _version++;
            _documents[namespaceName] = new ConfigurationDocument(_version.ToString(CultureInfo.InvariantCulture), new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<ConfigurationDocument>>> FetchAsync(string namespaceName, string? knownVersion, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Fetches++;
            if (Throw)
            {
                throw new InvalidOperationException("The remote configuration is broken.");
            }

            if (Fail is { } error)
            {
                return ValueTask.FromResult<Result<Maybe<ConfigurationDocument>>>(error);
            }

            if (!_documents.TryGetValue(namespaceName, out var document))
            {
                return ValueTask.FromResult<Result<Maybe<ConfigurationDocument>>>(Error.NotFound(ConfigurationErrors.NamespaceNotFound, ConfigurationMessages.NamespaceNotFound, MessageArguments.Of(("namespace", namespaceName))));
            }

            return ValueTask.FromResult(Result.Success(knownVersion == document.Version ? default : Maybe.Some(document)));
        }
    }
}
