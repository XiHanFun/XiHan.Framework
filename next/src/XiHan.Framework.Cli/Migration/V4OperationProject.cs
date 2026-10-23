// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-06 在一个项目里生成操作时的汇总：序列化上下文要登记的类型、要引入的命名空间、是否需要空响应。
/// </summary>
/// <param name="name">项目名（<c>.csproj</c> 文件名，作为生成代码的根命名空间）。</param>
/// <param name="newLine">项目文件的换行符。</param>
internal sealed class V4OperationProject(string name, string newLine)
{
    /// <summary>
    /// 项目名。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 换行符。
    /// </summary>
    public string NewLine { get; } = newLine;

    /// <summary>
    /// 请求与响应类型（源码写法），字典序。
    /// </summary>
    public SortedSet<string> Types { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 序列化上下文要引入的命名空间：服务文件的 using、服务命名空间与各操作命名空间。
    /// </summary>
    public SortedSet<string> Usings { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 有返回 <c>Task</c> / <c>void</c> 的方法，需要生成 <c>EmptyResponse</c>。
    /// </summary>
    public bool NeedsEmptyResponse { get; set; }
}
