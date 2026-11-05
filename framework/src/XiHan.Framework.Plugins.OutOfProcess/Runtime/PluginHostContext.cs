// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Plugins.OutOfProcess.Callbacks;
using XiHan.Framework.Plugins.Protocol.Handshake;

namespace XiHan.Framework.Plugins.OutOfProcess.Runtime;

/// <summary>
/// 宿主侧所有插件共享的环境
/// </summary>
/// <param name="HostEndpoint">宿主回调端点</param>
/// <param name="Primitives">宿主原语层大版本</param>
/// <param name="DefaultLocale">宿主默认区域</param>
/// <param name="Application">应用名；可空</param>
/// <param name="WorkRoot">插件工作目录的根</param>
/// <param name="Options">选项</param>
/// <param name="Configuration">宿主配置（插件配置节与绑定从中读取）</param>
/// <param name="Clock">时钟</param>
/// <param name="Sessions">会话登记表</param>
/// <param name="StopGrace">停止宽限</param>
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
    TimeSpan StopGrace)
{
    /// <summary>
    /// 时钟
    /// </summary>
    /// <remarks>
    /// 配置阶段还没有容器，先用构造时给的；宿主附着到容器后换成容器里的 <see cref="TimeProvider"/>，重启退避、健康轮询与停止等待都按它计时
    /// </remarks>
    public TimeProvider Clock { get; set; } = Clock;
}
