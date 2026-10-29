// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace KubernetesApp;

/// <summary>
/// 示例选项。
/// </summary>
/// <remarks>
/// 横幅文本，从配置节 <c>Sample</c> 绑定，ConfigMap 更新后随 <c>IOptionsMonitor</c> 重载。
/// </remarks>
public sealed class BannerOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string Section = "Sample";

    /// <summary>
    /// 横幅文本。
    /// </summary>
    public string Banner { get; set; } = "Hello from XiHan";
}
