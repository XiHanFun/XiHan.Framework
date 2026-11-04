// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 记录是否被释放的远程配置客户端
/// </summary>
/// <remarks>
/// 总是返回同一份文档（<c>Greeting = hello</c>，版本 <c>1</c>）；释放之后取值抛出 <see cref="ObjectDisposedException"/>，与持有连接的真实客户端一样
/// </remarks>
internal sealed class DisposableRemoteConfiguration : IRemoteConfigurationClient, IDisposable
{
    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<ConfigurationDocument>>> FetchAsync(string namespaceName, string? knownVersion, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        var document = new ConfigurationDocument("1", new Dictionary<string, string?> { ["Greeting"] = "hello" });
        return ValueTask.FromResult(Result.Success(knownVersion == document.Version ? default : Maybe.Some(document)));
    }

    /// <inheritdoc />
    public void Dispose() => Disposed = true;
}
