// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Abstractions.Tests;

/// <summary>
/// 测试用文档类型
/// </summary>
/// <remarks>
/// 刻意不重写相等性：抽象包里的记录类型对文档一律按引用比较，
/// 用值相等的文档类型会掩盖这一语义。
/// </remarks>
public class SearchTestDocument
{
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 浏览量
    /// </summary>
    public int Views { get; set; }
}
