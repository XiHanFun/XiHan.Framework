// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件上下文，为插件提供隔离的执行环境。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPluginContext
{
    /// <summary>
    /// 插件名称。
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// 插件的隔离服务提供器。
    /// </summary>
    IServiceProvider Services { get; }
}
