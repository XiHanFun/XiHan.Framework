// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;
using XiHan.Framework.Domain.Shared.Paging.Validators;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Validators;

/// <summary>
/// 分页验证器的测试
/// </summary>
public class PageValidatorTests
{
    /// <summary>
    /// 传入空请求必须抛出参数空异常
    /// </summary>
    [Fact]
    public void ValidatePageRequest_NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PageValidator.ValidatePageRequest(null!));
    }

    /// <summary>
    /// 包含无效过滤条件时必须给出错误
    /// </summary>
    [Fact]
    public void ValidatePageRequest_WithInvalidFilter_ReportsError()
    {
        var request = new PageRequestDtoBase();
        request.Conditions.AddFilter(new QueryFilter());

        var result = PageValidator.ValidatePageRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("过滤条件 [0] 无效"));
    }

    /// <summary>
    /// 包含无效排序条件时必须给出错误
    /// </summary>
    [Fact]
    public void ValidatePageRequest_WithInvalidSort_ReportsError()
    {
        var request = new PageRequestDtoBase();
        request.Conditions.AddSort(new QuerySort());

        var result = PageValidator.ValidatePageRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("排序条件 [0] 无效"));
    }

    /// <summary>
    /// 指定了关键字但未指定搜索字段时必须给出错误
    /// </summary>
    [Fact]
    public void ValidatePageRequest_WithKeywordButNoFields_ReportsError()
    {
        var request = new PageRequestDtoBase().WithKeyword("zhang");

        var result = PageValidator.ValidatePageRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("指定了关键字但未指定搜索字段"));
    }

    /// <summary>
    /// 合法请求必须通过验证
    /// </summary>
    [Fact]
    public void ValidatePageRequest_ValidRequest_IsValid()
    {
        var request = new PageRequestDtoBase()
            .WithFilter("Age", 30)
            .WithSort("Name");

        var result = PageValidator.ValidatePageRequest(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
