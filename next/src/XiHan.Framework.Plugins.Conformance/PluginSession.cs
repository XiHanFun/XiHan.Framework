// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// 一个已握手的被测插件进程。
/// </summary>
/// <param name="Process">进程。</param>
/// <param name="Manifest">目录里的清单。</param>
/// <param name="Handshake">核对后的握手结果。</param>
internal sealed record PluginSession(PluginProcess Process, Manifest Manifest, HandshakeInfo Handshake) : IAsyncDisposable
{
    /// <summary>
    /// 插件服务客户端。
    /// </summary>
    public Plugin.PluginClient Client => Process.Client;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Process.DisposeAsync();
}
