// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Models;

/// <summary>
/// 关键字搜索模型的测试
/// </summary>
public class QueryKeywordTests
{
    /// <summary>
    /// 添加字段必须忽略大小写去重
    /// </summary>
    [Fact]
    public void AddField_DeduplicatesCaseInsensitively()
    {
        var keyword = new QueryKeyword();
        keyword.AddField("Name").AddField("NAME");

        Assert.Single(keyword.Fields);
        Assert.Equal("Name", keyword.Fields[0]);
    }

    /// <summary>
    /// 克隆必须保留关键字值、字段列表与匹配模式
    /// </summary>
    [Fact]
    public void Clone_PreservesValue_Fields_AndMatchMode()
    {
        var keyword = new QueryKeyword { Value = "zhang", Fields = ["Name", "Title"], MatchMode = KeywordMatchMode.Exact };

        var clone = keyword.Clone();

        Assert.Equal("zhang", clone.Value);
        Assert.Equal(["Name", "Title"], clone.Fields);
        Assert.Equal(KeywordMatchMode.Exact, clone.MatchMode);
    }

    /// <summary>
    /// 批量添加字段必须跳过空值
    /// </summary>
    [Fact]
    public void AddFields_SkipsNullAndBlank()
    {
        var keyword = new QueryKeyword();
        keyword.AddFields(["Name", null!, "   ", "Title"]);

        Assert.Equal(["Name", "Title"], keyword.Fields);
    }
}
