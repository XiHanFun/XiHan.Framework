// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Domain.Shared.Paging.Attributes;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Reflection;
using XiHan.Framework.Domain.Shared.Tests.Samples;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Reflection;

/// <summary>
/// 特性读取器的测试
/// </summary>
public class AttributeReaderTests
{
    /// <summary>
    /// 读取查询字段必须包含别名并正确映射到同一属性
    /// </summary>
    [Fact]
    public void GetQueryFields_ReadsAlias_AndAttributeSettings()
    {
        var fields = AttributeReader.GetQueryFields<QuerySampleEntity>();

        Assert.True(fields.ContainsKey("Name"));
        Assert.True(fields.ContainsKey("userName"));
        Assert.Same(fields["Name"], fields["userName"]);

        var name = fields["Name"];
        Assert.Equal("Name", name.PropertyName);
        Assert.Equal("userName", name.Alias);
        Assert.True(name.AllowFilter);
        Assert.True(name.AllowSort);
        Assert.Equal(1, name.Priority);

        Assert.False(fields["Age"].AllowFilter);
        Assert.False(fields["CreatedAt"].AllowSort);
    }

    /// <summary>
    /// 读取查询字段必须正确解析关键字搜索配置
    /// </summary>
    [Fact]
    public void GetQueryFields_ReadsKeywordSearchConfiguration()
    {
        var fields = AttributeReader.GetQueryFields<QuerySampleEntity>();

        var title = fields["Title"];
        Assert.True(title.KeywordSearchEnabled);
        Assert.Equal(KeywordMatchMode.StartsWith, title.KeywordMatchMode);
        Assert.Equal(1, title.KeywordPriority);
        Assert.True(title.IncludeInDefaultKeywordSearch);
    }

    /// <summary>
    /// 操作符支持判断必须结合显式配置与类型推断
    /// </summary>
    [Fact]
    public void IsOperatorSupported_UsesConfiguredOrInferredOperators()
    {
        Assert.False(AttributeReader.IsOperatorSupported<QuerySampleEntity>("Code", QueryOperator.GreaterThan));
        Assert.True(AttributeReader.IsOperatorSupported<QuerySampleEntity>("Code", QueryOperator.Contains));
        Assert.True(AttributeReader.IsOperatorSupported<QuerySampleEntity>("Age", QueryOperator.GreaterThan));
        Assert.False(AttributeReader.IsOperatorSupported<QuerySampleEntity>("IsActive", QueryOperator.Contains));
    }

    /// <summary>
    /// 默认关键字搜索字段必须返回启用且参与默认搜索的字段
    /// </summary>
    [Fact]
    public void GetDefaultKeywordFields_ReturnsEnabledFields()
    {
        var fields = AttributeReader.GetDefaultKeywordFields<QuerySampleEntity>();

        Assert.Equal(["Title"], fields);
    }

    /// <summary>
    /// 特性目标必须限定在属性上，且默认命名参数取值稳定
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsProperty_AndDefaultsAreStable()
    {
        var usage = typeof(QueryFieldAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Property, usage!.ValidOn);

        var queryField = new QueryFieldAttribute();
        Assert.Equal(string.Empty, queryField.Alias);
        Assert.True(queryField.AllowFilter);
        Assert.True(queryField.AllowSort);
        Assert.Equal(0, queryField.Priority);

        var keywordSearch = new KeywordSearchAttribute(KeywordMatchMode.Exact, 5);
        Assert.Equal(KeywordMatchMode.Exact, keywordSearch.MatchMode);
        Assert.Equal(5, keywordSearch.Priority);
        Assert.True(keywordSearch.Enabled);
        Assert.True(keywordSearch.IncludeInDefault);
        Assert.Equal(string.Empty, keywordSearch.Alias);
    }
}
