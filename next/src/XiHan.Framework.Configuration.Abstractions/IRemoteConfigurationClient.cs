// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 远程配置客户端（RFC-0014）：按命名空间取整份带版本的文档。实现可以长轮询：<c>knownVersion</c> 未变化时阻塞到变化或自己的窗口到期，收到取消时返回无值而不是抛出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IRemoteConfigurationClient
{
    /// <summary>
    /// 取命名空间的当前文档。<paramref name="knownVersion"/> 为空时总是返回文档；非空且远端未变化时返回无值。
    /// 命名空间不存在 → <see cref="ConfigurationErrors.NamespaceNotFound"/>；不可达 → <see cref="ConfigurationErrors.RemoteUnavailable"/>。
    /// </summary>
    ValueTask<Result<Maybe<ConfigurationDocument>>> FetchAsync(string namespaceName, string? knownVersion, CancellationToken cancellationToken);
}
