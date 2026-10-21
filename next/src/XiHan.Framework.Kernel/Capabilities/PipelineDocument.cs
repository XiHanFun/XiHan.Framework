// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 中 <c>contributes.pipeline</c> 一项的 JSON 形态。阶段以清单小写名承载。
/// </summary>
internal sealed class PipelineDocument
{
    /// <summary>
    /// 中间件类型的完全限定名。
    /// </summary>
    public string? Middleware { get; set; }

    /// <summary>
    /// 所属阶段的清单小写名。
    /// </summary>
    public string? Stage { get; set; }

    /// <summary>
    /// 必须位于这些中间件之前（完全限定名）。
    /// </summary>
    public List<string>? Before { get; set; }

    /// <summary>
    /// 必须位于这些中间件之后（完全限定名）。
    /// </summary>
    public List<string>? After { get; set; }
}
