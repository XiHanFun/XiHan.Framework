// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>keyword-matching.json</c>：列表查询关键字的折叠与匹配
/// </summary>
/// <remarks>
/// 规范 RFC-0021「定义」的「关键字」（ADR-0096、ADR-0099、ADR-0277）：关键字去掉首尾空白，为空不过滤；关键字与字段都折叠（不变文化的小写，U+001F 换成空格）之后按序数判断包含，任一字段包含即匹配，不跨字段，通配字符按字面。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class WorkflowKeywordVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly KeywordVectors _vectors = ConformanceVectors.Load<KeywordVectors>("keyword-matching.json");

    [Fact]
    public void FieldSeparator_IsTheOneInTheVectors()
        => Assert.Equal(_vectors.FieldSeparator, WorkflowKeyword.FieldSeparator.ToString());

    [Fact]
    public void Normalize_FoldsEveryKeyword()
    {
        Assert.NotEmpty(_vectors.Normalize);
        var failures = new List<string>();
        foreach (var vector in _vectors.Normalize)
        {
            var normalized = WorkflowKeyword.Normalize(vector.Keyword);
            if (!string.Equals(normalized, vector.Normalized, StringComparison.Ordinal))
            {
                failures.Add($"{vector.Name}: normalized to '{normalized ?? "null"}', the vector has '{vector.Normalized ?? "null"}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Matches_DecidesEveryCase()
    {
        Assert.NotEmpty(_vectors.Matches);
        var failures = new List<string>();
        foreach (var vector in _vectors.Matches)
        {
            // 匹配用例的关键字都不为空：规范化为空表示不过滤，那不是「匹配与否」的用例
            var keyword = WorkflowKeyword.Normalize(vector.Keyword);
            Assert.NotNull(keyword);

            // 内存实现逐个字段判断；数据库实现把各字段折叠后以分隔符拼成一列、对整列判断包含。两条路对同一个用例的裁定必须一样
            var perField = WorkflowKeyword.Matches(keyword, vector.Fields);
            var joined = WorkflowKeyword.Join(vector.Fields).Contains(keyword, StringComparison.Ordinal);
            if (perField != vector.Matches || joined != vector.Matches)
            {
                failures.Add($"{vector.Name}: field by field {perField}, on the joined column {joined}, the vector has {vector.Matches}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTheFoldingAndTheLiteralCharacters()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：没有给关键字、只有空白、带分隔符的关键字都要有
        Assert.Contains(_vectors.Normalize, static vector => vector.Keyword is null && vector.Normalized is null);
        Assert.Contains(_vectors.Normalize, static vector => vector.Keyword is { Length: > 0 } && vector.Normalized is null);
        Assert.Contains(_vectors.Normalize, static vector => vector.Keyword is not null && vector.Keyword.Contains(WorkflowKeyword.FieldSeparator));
        Assert.Contains(_vectors.Normalize, static vector => vector.Keyword is not null && vector.Normalized is not null && vector.Keyword.Trim() != vector.Normalized);

        // 匹配：两种结果、没有值的字段、带分隔符的字段、不跨字段
        Assert.Contains(_vectors.Matches, static vector => vector.Matches);
        Assert.Contains(_vectors.Matches, static vector => !vector.Matches && vector.Fields.Length > 1);
        Assert.Contains(_vectors.Matches, static vector => vector.Fields.Any(static field => field is null));
        Assert.Contains(_vectors.Matches, static vector => vector.Fields.Any(static field => field is not null && field.Contains(WorkflowKeyword.FieldSeparator)));

        // 按字面匹配的四个字符都要有命中的用例；SQL 的 LIKE 当成通配的三个还要有「当通配才命中」的用例，按字面不命中
        Assert.All<char>(['%', '_', '[', '\\'], static literal => Assert.Contains(_vectors.Matches, vector => vector.Matches && vector.Keyword.Contains(literal)));
        Assert.All<char>(['%', '_', '['], static wildcard => Assert.Contains(_vectors.Matches, vector => !vector.Matches && vector.Keyword.Contains(wildcard)));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="FieldSeparator">多个字段拼成一列时的分隔符</param>
    /// <param name="Normalize">关键字规范化的用例</param>
    /// <param name="Matches">匹配的用例</param>
    private sealed record KeywordVectors(string FieldSeparator, NormalizeCase[] Normalize, MatchCase[] Matches);

    /// <summary>
    /// 一个规范化用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Keyword">调用方给的关键字；<see langword="null"/> 表示没有给</param>
    /// <param name="Normalized">折叠之后用来匹配的文本；<see langword="null"/> 表示不按关键字过滤</param>
    private sealed record NormalizeCase(string Name, string? Keyword, string? Normalized);

    /// <summary>
    /// 一个匹配用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Keyword">调用方给的关键字</param>
    /// <param name="Fields">被搜的各个字段；<see langword="null"/> 表示这个字段没有值</param>
    /// <param name="Matches">是否匹配</param>
    private sealed record MatchCase(string Name, string Keyword, string?[] Fields, bool Matches);
}
