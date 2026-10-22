// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 资源请求与上限。
/// </summary>
internal sealed record ResourceSettings
{
    /// <summary>
    /// 请求。
    /// </summary>
    public ResourceQuantities? Requests { get; set; }

    /// <summary>
    /// 上限。
    /// </summary>
    public ResourceQuantities? Limits { get; set; }
}
