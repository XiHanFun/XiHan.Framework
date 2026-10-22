// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Conformance;

/// <summary>
/// 远程配置套件的夹具：被测客户端与向远端发布文档的钩子。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class RemoteConfigurationFixture
{
    /// <summary>
    /// 被测客户端。
    /// </summary>
    public abstract IRemoteConfigurationClient Client { get; }

    /// <summary>
    /// 向远端发布（创建或整体替换）命名空间的键值；返回后客户端应能取到新版本。
    /// </summary>
    public abstract Task PublishAsync(string namespaceName, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken);
}
