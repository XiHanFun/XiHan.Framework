// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Protocol.Handshake;

namespace XiHan.Framework.Plugins.OutOfProcess.Processes;

/// <summary>
/// 启动一个插件进程所需的全部参数。
/// </summary>
/// <param name="PluginId">清单 ID。</param>
/// <param name="Executable">可执行文件的绝对路径。</param>
/// <param name="Directory">插件目录（工作目录）。</param>
/// <param name="SessionToken">会话令牌。</param>
/// <param name="HostEndpoint">宿主回调端点。</param>
/// <param name="InstanceId">实例 ID。</param>
/// <param name="Primitives">宿主原语层大版本。</param>
/// <param name="Locale">宿主默认区域。</param>
/// <param name="HandshakeTimeout">读握手行的时限。</param>
internal sealed record PluginLaunch(
    string PluginId,
    string Executable,
    string Directory,
    string SessionToken,
    PluginEndpoint HostEndpoint,
    string InstanceId,
    int Primitives,
    string Locale,
    TimeSpan HandshakeTimeout);
