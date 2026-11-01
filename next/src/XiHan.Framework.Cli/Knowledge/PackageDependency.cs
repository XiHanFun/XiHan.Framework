// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个第三方包依赖。
/// </summary>
internal sealed record PackageDependency
{
    /// <summary>
    /// 包 ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 中央包管理里登记的版本。
    /// </summary>
    public string Version { get; set; } = string.Empty;
}
