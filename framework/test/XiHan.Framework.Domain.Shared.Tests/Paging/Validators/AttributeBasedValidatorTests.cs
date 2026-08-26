// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Validators;
using XiHan.Framework.Domain.Shared.Tests.Samples;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Validators;

/// <summary>
/// 基于特性的验证器的测试
/// </summary>
public class AttributeBasedValidatorTests
{
    /// <summary>
    /// 合法请求必须通过验证
    /// </summary>
    [Fact]
    public void ValidatePageRequest_ValidRequest_IsValid()
    {
        var request = new PageRequestDtoBase()
            .WithFilter("Name", "Zhang")
            .WithSort("Name");

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 对不允许过滤的字段做过滤时必须给出错误
    /// </summary>
    [Fact]
    public void Filter_OnNonFilterableField_ReportsError()
    {
        var request = new PageRequestDtoBase().WithFilter("Age", 30);

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("'Age' 不允许过滤"));
    }

    /// <summary>
    /// 对不允许排序的字段做排序时必须给出错误
    /// </summary>
    [Fact]
    public void Sort_OnNonSortableField_ReportsError()
    {
        var request = new PageRequestDtoBase().WithSort("CreatedAt");

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("'CreatedAt' 不允许排序"));
    }

    /// <summary>
    /// 使用字段不支持的操作符时必须给出错误
    /// </summary>
    [Fact]
    public void Filter_WithUnsupportedOperator_ReportsError()
    {
        var request = new PageRequestDtoBase().WithFilter("Code", "a", QueryOperator.GreaterThan);

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("不支持操作符 'GreaterThan'"));
    }

    /// <summary>
    /// 使用不存在的字段名时必须给出错误
    /// </summary>
    [Fact]
    public void Filter_WithUnknownField_ReportsError()
    {
        var request = new PageRequestDtoBase().WithFilter("NonExistent", "x");

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("'NonExistent' 不存在或不可用"));
    }

    /// <summary>
    /// 关键字搜索字段为非字符串类型时必须给出错误
    /// </summary>
    [Fact]
    public void Keyword_OnNonStringField_ReportsError()
    {
        var request = new PageRequestDtoBase().WithKeyword("zhang", "Age");

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("关键字搜索字段 'Age' 必须是字符串类型"));
    }

    /// <summary>
    /// 通过别名过滤必须解析到实际属性并通过验证
    /// </summary>
    [Fact]
    public void Filter_ByAlias_ResolvesToProperty()
    {
        var request = new PageRequestDtoBase().WithFilter("userName", "Zhang");

        var result = AttributeBasedValidator.ValidatePageRequest<QuerySampleEntity>(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
