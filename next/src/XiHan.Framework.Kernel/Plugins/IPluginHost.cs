// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件宿主。
/// </summary>
/// <remarks>
/// 加载、启动、停止、卸载插件并上报状态。具体隔离机制由 <c>Plugins.*</c> 包实现。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IPluginHost
{
    /// <summary>
    /// 当前已加载的插件。
    /// </summary>
    IReadOnlyList<PluginStatus> Plugins { get; }

    /// <summary>
    /// 加载并启动插件。
    /// </summary>
    /// <remarks>
    /// 失败以 <see cref="Result{T}"/> 返回，不抛出。
    /// </remarks>
    Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken);

    /// <summary>
    /// 停止并卸载插件。
    /// </summary>
    Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken);
}
