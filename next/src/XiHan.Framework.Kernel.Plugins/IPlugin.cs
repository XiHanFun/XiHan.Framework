// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件契约。
/// 声明插件的名称、版本和依赖。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPlugin
{
    /// <summary>
    /// 插件名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本。
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 插件依赖的其他插件名称。
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }
}
