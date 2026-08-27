// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Docs.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Docs.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 Web 文档服务集合扩展测试
/// </summary>
/// <remarks>
/// 断言落在"注册结果"而不是"调用了哪些方法"：默认文档 v1 必须存在且不带分组过滤；
/// 每个启用的动态 API 分组各自注册一份独立的 OpenApi 文档，且它的 ShouldInclude 只放行本分组；
/// 被禁用/被隐藏的分组不得注册文档。分组取样服务见 Swagger/DocsGroupSampleServices.cs。
/// </remarks>
public class XiHanWebDocsServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanWebDocs_WhenServicesNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => XiHanWebDocsServiceCollectionExtensions.AddXiHanWebDocs(null!));

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 返回同一个服务集合实例，保证链式注册
    /// </summary>
    [Fact]
    public void AddXiHanWebDocs_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanWebDocs());
    }

    /// <summary>
    /// 默认文档 v1 被注册，且不带分组过滤（未分组的接口必须进默认文档）
    /// </summary>
    [Fact]
    public void AddXiHanWebDocs_RegistersDefaultDocument()
    {
        var options = ResolveOpenApiOptions("v1");

        Assert.Equal("v1", options.DocumentName);

        var shouldInclude = options.ShouldInclude!;
        Assert.True(shouldInclude(new ApiDescription { GroupName = null }));
    }

    /// <summary>
    /// 每个启用的动态 API 分组各注册一份独立文档
    /// </summary>
    /// <param name="documentName">分组文档名</param>
    [Theory]
    [InlineData("docs-alpha")]
    [InlineData("docs-beta")]
    [InlineData("docs-method")]
    [InlineData("docs-ordered")]
    public void AddXiHanWebDocs_RegistersDocumentPerEnabledGroup(string documentName)
    {
        Assert.Equal(documentName, ResolveOpenApiOptions(documentName).DocumentName);
    }

    /// <summary>
    /// 分组文档的过滤谓词只放行本分组的接口
    /// </summary>
    /// <remarks>
    /// foreach 里给每个分组注册一份 options，闭包一旦捕获错变量，所有分组文档都会被同一个过滤器串味。
    /// </remarks>
    [Fact]
    public void AddXiHanWebDocs_GroupDocumentIncludesOnlyItsOwnGroup()
    {
        var shouldInclude = ResolveOpenApiOptions("docs-alpha").ShouldInclude!;

        Assert.True(shouldInclude(new ApiDescription { GroupName = "docs-alpha" }));
        Assert.False(shouldInclude(new ApiDescription { GroupName = "docs-beta" }));
        Assert.False(shouldInclude(new ApiDescription { GroupName = null }));
    }

    /// <summary>
    /// 分组过滤谓词忽略大小写匹配分组名
    /// </summary>
    [Fact]
    public void AddXiHanWebDocs_GroupFilterIsCaseInsensitive()
    {
        var shouldInclude = ResolveOpenApiOptions("docs-alpha").ShouldInclude!;

        Assert.True(shouldInclude(new ApiDescription { GroupName = "DOCS-ALPHA" }));
    }

    /// <summary>
    /// 被禁用或被隐藏的分组不注册文档
    /// </summary>
    /// <param name="documentName">分组文档名</param>
    [Theory]
    [InlineData("docs-disabled")]
    [InlineData("docs-hidden")]
    public void AddXiHanWebDocs_DoesNotRegisterDocumentForExcludedGroup(string documentName)
    {
        Assert.NotEqual(documentName, ResolveOpenApiOptions(documentName).DocumentName);
    }

    /// <summary>
    /// 注册后解析指定文档名的 OpenApi 选项
    /// </summary>
    /// <param name="documentName">文档名</param>
    /// <returns>OpenApi 选项</returns>
    private static OpenApiOptions ResolveOpenApiOptions(string documentName)
    {
        var services = new ServiceCollection();

        // AddOpenApi 自己也会拉起选项基础设施，这里显式补一次，避免断言被"选项没注册"这种无关原因带偏
        services.AddOptions();
        services.AddXiHanWebDocs();

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentName);
    }
}
