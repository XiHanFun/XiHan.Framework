// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 列表排序用的键
/// </summary>
/// <remarks>
/// 定义与实例查询按标题排序时，比较的是标题的前 <see cref="MaxTitleLength"/> 个字符（ADR-0102）：显示名没有长度上限，数据库实现要把排序键存进定长的列（长文本在 Oracle 上不能排序），内存实现按同一个键比较，两者结果才一致。前 <see cref="MaxTitleLength"/> 个字符相同的，按次序键决定先后
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowSortKeys
{
    /// <summary>
    /// 标题排序键的最大长度（UTF-16 字符）
    /// </summary>
    /// <remarks>
    /// 数据库实现的 <c>Title</c> 列就是这个宽度
    /// </remarks>
    public const int MaxTitleLength = 256;

    /// <summary>
    /// 标题的排序键：前 <see cref="MaxTitleLength"/> 个字符
    /// </summary>
    /// <remarks>
    /// 不拆开代理对：截断处是高位代理时少取一个字符，存进数据库的值总是合法的 UTF-16
    /// </remarks>
    public static string Title(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (title.Length <= MaxTitleLength)
        {
            return title;
        }

        // 截在代理对中间会留下孤立的高位代理，UTF-8 编码的库（PostgreSQL 等）写不进，所以退一个字符
        var length = char.IsHighSurrogate(title[MaxTitleLength - 1]) ? MaxTitleLength - 1 : MaxTitleLength;
        return title[..length];
    }
}
