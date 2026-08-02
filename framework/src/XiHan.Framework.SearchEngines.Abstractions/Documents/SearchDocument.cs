// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Documents;

/// <summary>
/// 带标识的待索引文档
/// </summary>
/// <typeparam name="TDocument">文档类型</typeparam>
/// <param name="Id">文档标识，同一索引内唯一，重复写入按标识整体覆盖</param>
/// <param name="Document">文档</param>
public sealed record SearchDocument<TDocument>(string Id, TDocument Document)
    where TDocument : class
{
    /// <summary>
    /// 文档标识
    /// </summary>
    public string Id { get; } = string.IsNullOrWhiteSpace(Id)
        ? throw new ArgumentException("文档标识不能为空。", nameof(Id))
        : Id;

    /// <summary>
    /// 文档
    /// </summary>
    public TDocument Document { get; } = Document ?? throw new ArgumentNullException(nameof(Document));
}
