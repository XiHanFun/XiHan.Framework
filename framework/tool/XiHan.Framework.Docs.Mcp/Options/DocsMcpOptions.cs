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
    /// 判定「这个问题在问我们的文档吗」的阈值：查询里语料认识的词条，其 IDF 之和至少要占全部词条 IDF 之和的这个比例
    /// </summary>
    /// <remarks>
    /// 解决的问题：中文 bigram 在「怎么」「配置」这类高频片段上必然与文档有交集，
    /// 所以「零命中」几乎不会发生——问「红烧肉的做法」也能拿回 5 个授权/缓存章节，
    /// 而不是那句「文档中没有，请不要基于猜测作答」。绝对分数一刀切解决不了：
    /// 实测不相关的「怎么用 Rust 写异步运行时」得分 0.900，高于真正相关的「怎么配置缓存过期时间」的 0.745。
    /// <para>
    /// 阈值怎么标定出来的：在真实语料（163 篇、1720 个章节）上跑 26 条相关查询与 18 条不相关查询，
    /// 本判据在相关查询上全部取到 1.000，不相关查询最高 0.793（「PostgreSQL 的 VACUUM 什么时候触发」），
    /// 中间空出 [0.793, 1.000] 的间隔。0.90 取的是这个间隔的中点，两侧各留约 0.10 余量，
    /// 不是凑合让某几条查询变绿的魔数。改这个值必须重跑黄金查询集。
    /// </para>
    /// </remarks>
    public double MinKnownTermCoverage { get; init; } = 0.90;

    /// <summary>
    /// 拉丁词条要出现在至少这么多个章节里，才算「语料认识它」
    /// </summary>
    /// <remarks>
    /// 中文 bigram 是分词器切出来的子词片段，只出现一次不代表任何事；
    /// 拉丁词条是完整的单词或标识符，只出现一次通常是顺带提及。
    /// 实测：<c>Rust</c> 在整个语料里只出现于 utils.md 一份伪堆栈模板的语言清单中，
    /// <c>TLS</c> 只出现于 bot-email.md 的一行配置说明。按「出现过就算认识」判定，
    /// 「怎么用 Rust 写异步运行时」的覆盖率会是 1.000，与真正相关的查询无法区分。
    /// </remarks>
    public int MinLatinTermDocumentFrequency { get; init; } = 2;

    /// <summary>
    /// 语料至少要有这么多章节，相关性截断才生效
    /// </summary>
    /// <remarks>
    /// 判据建立在 IDF 之上，而 IDF 在只有几十个章节的语料上完全不可用——
    /// 那时每个词看起来都很罕见，截断会把所有查询都判成不相关。
    /// 真实语料有 1720 个章节，这道下限只会影响测试夹具之类的极小语料。
    /// </remarks>
    public int MinSectionsForRelevanceCutoff { get; init; } = 200;

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
