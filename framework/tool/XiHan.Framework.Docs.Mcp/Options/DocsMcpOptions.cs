// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Options;

/// <summary>
/// 文档 MCP 服务端的可调参数
/// </summary>
public sealed class DocsMcpOptions
{
    /// <summary>
    /// 标题命中的加权倍数，标题是人工撰写的最强信号
    /// </summary>
    public double TitleBoost { get; init; } = 3.0;

    /// <summary>
    /// 同一文件最多返回的章节数，防止一篇文章洗掉整个结果列表
    /// </summary>
    public int MaxSectionsPerFile { get; init; } = 2;

    /// <summary>
    /// 检索结果的默认条数
    /// </summary>
    public int DefaultLimit { get; init; } = 5;

    /// <summary>
    /// 检索结果的条数上限，超出时截断而非报错
    /// </summary>
    public int MaxLimit { get; init; } = 15;

    /// <summary>
    /// 热更新检查的节流间隔，两次查询间隔小于此值时跳过 mtime 扫描
    /// </summary>
    public TimeSpan RefreshThrottle { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 单篇文档整体返回的字符数上限，超出时改为返回章节目录
    /// </summary>
    public int MaxWholeDocumentLength { get; init; } = 30 * 1024;

    /// <summary>
    /// 各来源的权重，指南略高是因为任务导向的提问占多数
    /// </summary>
    public IReadOnlyDictionary<DocSourceKind, double> SourceWeights { get; init; } =
        new Dictionary<DocSourceKind, double>
        {
            [DocSourceKind.Guide] = 1.2,
            [DocSourceKind.Package] = 1.0,
            [DocSourceKind.Root] = 0.9,
            [DocSourceKind.PackageReadme] = 0.8
        };
}
