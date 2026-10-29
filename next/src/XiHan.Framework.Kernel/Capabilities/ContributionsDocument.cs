// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 中 <c>contributes</c> 的 JSON 形态。
/// </summary>
/// <remarks>
/// 各列表缺省表示没有对应贡献。
/// </remarks>
internal sealed class ContributionsDocument
{
    /// <summary>
    /// 管道中间件。
    /// </summary>
    public List<PipelineDocument>? Pipeline { get; set; }

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public List<string>? Operations { get; set; }

    /// <summary>
    /// 配置节。
    /// </summary>
    public ConfigDocument? Config { get; set; }

    /// <summary>
    /// 此包定义的权限码。
    /// </summary>
    public List<string>? Permissions { get; set; }

    /// <summary>
    /// 健康项名称。
    /// </summary>
    public List<string>? Health { get; set; }
}
