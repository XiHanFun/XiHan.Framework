// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 配置挂载。
/// </summary>
internal sealed record ConfigSettings
{
    /// <summary>
    /// 挂载目录。
    /// </summary>
    public string MountPath { get; set; } = "/etc/xihan";

    /// <summary>
    /// 进 ConfigMap 的文件名。
    /// </summary>
    public IReadOnlyList<string> Files { get; set; } = [];
}
