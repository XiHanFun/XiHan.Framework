// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个正式包
/// </summary>
/// <remarks>
/// 包 ID、说明与依赖取自项目文件，环层与一句话定位取自 <c>next/README.md</c> 的包表
/// </remarks>
internal sealed record PackageEntry
{
    /// <summary>
    /// 包 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 环层（<c>0</c> ~ <c>6</c> 或「工具链」）
    /// </summary>
    public string? Ring { get; set; }

    /// <summary>
    /// 包表里的一句话定位
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 项目文件里的包说明
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 引用的框架包
    /// </summary>
    public IReadOnlyList<string> References { get; set; } = [];

    /// <summary>
    /// 引用的第三方包与版本
    /// </summary>
    public IReadOnlyList<PackageDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// 引用的共享框架，如 <c>Microsoft.AspNetCore.App</c>
    /// </summary>
    public IReadOnlyList<string> FrameworkReferences { get; set; } = [];
}
