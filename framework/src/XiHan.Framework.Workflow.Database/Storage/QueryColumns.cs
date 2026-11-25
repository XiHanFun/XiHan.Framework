// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 查询列的比较
/// </summary>
/// <remarks>
/// 查询列是由文档算出、只为把过滤与排序下推到 SQL 而存的列（关键字、标题、状态序号、分类、所属实例的定义名与状态）。回填比较算出的值与行里现有的值，相同的行不改写（ADR-0304）
/// </remarks>
internal static class QueryColumns
{
    /// <summary>
    /// 两个文本列的值是否相同
    /// </summary>
    /// <remarks>
    /// 空串与 <see langword="null"/> 当作相同：Oracle 把空串存成 NULL，读回来的就不是写进去的空串，逐个码元比较会让这些行每次回填都被改写；这些列上两者的含义也相同（关键字不匹配空串，排序标题与分类不会是空串）
    /// </remarks>
    public static bool SameText(string? stored, string? computed)
        => string.Equals(stored ?? string.Empty, computed ?? string.Empty, StringComparison.Ordinal);
}
