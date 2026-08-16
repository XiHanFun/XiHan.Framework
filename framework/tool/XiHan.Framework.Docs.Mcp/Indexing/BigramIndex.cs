// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 倒排索引中的一条记录
/// </summary>
/// <param name="SectionId">章节在章节列表中的下标</param>
/// <param name="InTitle">该词条是否出现在章节标题中</param>
public readonly record struct Posting(int SectionId, bool InTitle);

/// <summary>
/// 词条到章节的倒排索引
/// </summary>
/// <remarks>
/// 同一章节内同一词条只保留一条记录，标题命中优先。
/// 这样打分时的「命中词数 ÷ 查询词总数」才是真正的覆盖率，不会被词频扭曲。
/// </remarks>
public sealed class BigramIndex
{
    private readonly Dictionary<string, List<Posting>> _postings = new(StringComparer.Ordinal);

    /// <summary>
    /// 把一个章节的标题与正文加入索引
    /// </summary>
    /// <param name="sectionId">章节下标</param>
    /// <param name="title">章节标题路径</param>
    /// <param name="body">章节正文</param>
    public void Add(int sectionId, string title, string body)
    {
        var titleTerms = Tokenizer.Tokenize(title).ToHashSet(StringComparer.Ordinal);

        foreach (var term in titleTerms)
        {
            AddPosting(term, new Posting(sectionId, InTitle: true));
        }

        foreach (var term in Tokenizer.Tokenize(body).ToHashSet(StringComparer.Ordinal))
        {
            if (!titleTerms.Contains(term))
            {
                AddPosting(term, new Posting(sectionId, InTitle: false));
            }
        }
    }

    /// <summary>
    /// 查找一个词条对应的全部章节
    /// </summary>
    /// <param name="term">词条</param>
    /// <returns>记录列表，未收录时为空集合</returns>
    public IReadOnlyList<Posting> Find(string term)
    {
        return _postings.TryGetValue(term, out var list) ? list : [];
    }

    /// <summary>
    /// 追加一条记录
    /// </summary>
    private void AddPosting(string term, Posting posting)
    {
        if (!_postings.TryGetValue(term, out var list))
        {
            list = [];
            _postings[term] = list;
        }

        list.Add(posting);
    }
}
