// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 应用清单 <c>xihan.app.json</c>（规范：<c>spec/packaging/app-manifest.md</c>）。
/// </summary>
/// <remarks>
/// 由 <see cref="AppManifestReader"/> 读取并校验，缺省值在这里。
/// </remarks>
internal sealed record AppManifest
{
    /// <summary>
    /// 名字：DNS 标签。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 相对清单所在目录的项目文件。
    /// </summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Profile：minimal / standard / full。
    /// </summary>
    public string Profile { get; set; } = "standard";

    /// <summary>
    /// 镜像构建。
    /// </summary>
    public BuildSettings Build { get; set; } = new();

    /// <summary>
    /// 镜像。
    /// </summary>
    public ImageSettings Image { get; set; } = new();

    /// <summary>
    /// 协议端口。
    /// </summary>
    public PortSettings Ports { get; set; } = new();

    /// <summary>
    /// 资源绑定，按名字。
    /// </summary>
    public IReadOnlyDictionary<string, BindingSettings> Bindings { get; set; } = new Dictionary<string, BindingSettings>(StringComparer.Ordinal);

    /// <summary>
    /// 配置挂载。
    /// </summary>
    public ConfigSettings Config { get; set; } = new();

    /// <summary>
    /// 探针。
    /// </summary>
    public ProbeSettings Probes { get; set; } = new();

    /// <summary>
    /// 停机。
    /// </summary>
    public ShutdownSettings Shutdown { get; set; } = new();

    /// <summary>
    /// 资源请求与上限。
    /// </summary>
    public ResourceSettings? Resources { get; set; }

    /// <summary>
    /// 副本数。
    /// </summary>
    public int Replicas { get; set; } = 1;

    /// <summary>
    /// 自动伸缩。
    /// </summary>
    public ScalingSettings? Scaling { get; set; }

    /// <summary>
    /// 领导选举。
    /// </summary>
    public LeaderSettings? Leader { get; set; }

    /// <summary>
    /// 环境变量。
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 镜像引用 <c>repository:tag</c>。
    /// </summary>
    public string ImageReference => (Image.Repository ?? Name) + ":" + Image.Tag;

    /// <summary>
    /// 项目文件名（不含扩展名），用作程序集名与 Aspire 的项目类型名。
    /// </summary>
    public string AssemblyName => System.IO.Path.GetFileNameWithoutExtension(Project);
}
