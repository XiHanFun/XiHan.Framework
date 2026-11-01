// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 贡献给管道的一个中间件。
/// </summary>
internal sealed record PipelineEntry
{
    /// <summary>
    /// 中间件类型的完全限定名。
    /// </summary>
    public string Middleware { get; set; } = string.Empty;

    /// <summary>
    /// 所在阶段。
    /// </summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// 必须位于这些中间件之前。
    /// </summary>
    public IReadOnlyList<string> Before { get; set; } = [];

    /// <summary>
    /// 必须位于这些中间件之后。
    /// </summary>
    public IReadOnlyList<string> After { get; set; } = [];
}
