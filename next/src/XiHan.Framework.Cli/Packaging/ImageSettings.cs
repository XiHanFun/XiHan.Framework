// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 镜像设置。
/// </summary>
internal sealed record ImageSettings
{
    /// <summary>
    /// 镜像仓库（可含注册表前缀）。
    /// </summary>
    /// <remarks>
    /// 为空时用应用名。
    /// </remarks>
    public string? Repository { get; set; }

    /// <summary>
    /// 标签。
    /// </summary>
    public string Tag { get; set; } = "latest";

    /// <summary>
    /// 是否 Native AOT 发布。
    /// </summary>
    public bool Aot { get; set; }
}
