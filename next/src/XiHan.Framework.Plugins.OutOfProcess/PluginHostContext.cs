// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 宿主侧所有插件共享的环境。
/// </summary>
/// <param name="HostEndpoint">宿主回调端点。</param>
/// <param name="Primitives">宿主原语层大版本。</param>
/// <param name="DefaultLocale">宿主默认区域。</param>
/// <param name="Application">应用名；可空。</param>
/// <param name="WorkRoot">插件工作目录的根。</param>
/// <param name="Options">选项。</param>
/// <param name="Configuration">宿主配置（插件配置节与绑定从中读取）。</param>
/// <param name="Clock">时钟。</param>
/// <param name="Sessions">会话登记表。</param>
/// <param name="StopGrace">停止宽限。</param>
internal sealed record PluginHostContext(
    PluginEndpoint HostEndpoint,
    int Primitives,
    string DefaultLocale,
    string Application,
    string WorkRoot,
    OutOfProcessPluginOptions Options,
    IConfiguration Configuration,
    TimeProvider Clock,
    PluginSessions Sessions,
    TimeSpan StopGrace);
