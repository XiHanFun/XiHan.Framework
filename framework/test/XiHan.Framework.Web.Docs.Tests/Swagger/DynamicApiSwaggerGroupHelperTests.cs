// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// 动态 API 文档分组辅助测试
/// </summary>
/// <remarks>
/// 两组契约：一是从 ApiExplorer 分组集合提取分组名（纯函数，用手写 fake 提供器驱动）；
/// 二是从 DynamicApiAttribute 扫描分组定义（会真实扫描当前进程已加载程序集，
/// 取样服务集中在 DocsGroupSampleServices.cs，分组键统一 docs- 前缀，用例按前缀过滤后做确定性断言）。
/// 辅助类本身是 internal，经 <see cref="SwaggerInternals"/> 反射调用。
/// </remarks>
public class DynamicApiSwaggerGroupHelperTests
{
    /// <summary>
    /// 默认文档名进 URL（/openapi/{name}.json），漂移会直接打断前端与网关的取址
    /// </summary>
    [Fact]
    public void DefaultDocName_IsV1()
    {
        Assert.Equal("v1", SwaggerInternals.DefaultDocName);
    }

    /// <summary>
    /// 默认文档标题是 UI 上的默认分组标题
    /// </summary>
    [Fact]
    public void DefaultDocTitle_IsApiV1()
    {
        Assert.Equal("API V1", SwaggerInternals.DefaultDocTitle);
    }

    /// <summary>
    /// 空分组名、纯空白分组名与 null 一律被剔除
    /// </summary>
    [Fact]
    public void GetGroupNames_WhenNameBlank_IsFiltered()
    {
        var provider = new FakeApiDescriptionGroupCollectionProvider(null, string.Empty, "   ", "beta", "alpha");

        Assert.Equal(new[] { "alpha", "beta" }, SwaggerInternals.GetGroupNames(provider).ToArray());
    }

    /// <summary>
    /// 分组名按忽略大小写去重，保留首次出现的写法
    /// </summary>
    [Fact]
    public void GetGroupNames_WhenNamesDifferOnlyByCase_KeepsFirstOccurrence()
    {
        var provider = new FakeApiDescriptionGroupCollectionProvider("Alpha", "alpha", "ALPHA");

        Assert.Equal("Alpha", Assert.Single(SwaggerInternals.GetGroupNames(provider)));
    }

    /// <summary>
    /// 分组名按忽略大小写升序排列，与输入顺序无关
    /// </summary>
    [Fact]
    public void GetGroupNames_SortsCaseInsensitiveAscending()
    {
        var provider = new FakeApiDescriptionGroupCollectionProvider("zeta", "Alpha", "mid");

        Assert.Equal(new[] { "Alpha", "mid", "zeta" }, SwaggerInternals.GetGroupNames(provider).ToArray());
    }

    /// <summary>
    /// 没有任何分组时返回空列表而不是 null
    /// </summary>
    [Fact]
    public void GetGroupNames_WhenNoGroups_ReturnsEmpty()
    {
        var provider = new FakeApiDescriptionGroupCollectionProvider();

        Assert.Empty(SwaggerInternals.GetGroupNames(provider));
    }

    /// <summary>
    /// 类级分组特性同时决定分组键、显示名与顺序
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_ReadsClassLevelGroup()
    {
        var definition = GetSampleDefinition("docs-alpha");

        Assert.Equal("字母分组", definition.DisplayName);
        Assert.Equal(3, definition.Order);
    }

    /// <summary>
    /// 分组键两侧空白被裁掉，未给显示名时显示名回退为分组键
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_TrimsGroupAndFallsBackDisplayName()
    {
        var definition = GetSampleDefinition("docs-beta");

        Assert.Equal("docs-beta", definition.Group);
        Assert.Equal("docs-beta", definition.DisplayName);
        Assert.Equal(0, definition.Order);
    }

    /// <summary>
    /// 同一分组的多个特性按 Order 合并，高 Order 覆盖低 Order
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_WhenSameGroupDeclaredTwice_HigherOrderWins()
    {
        var definition = GetSampleDefinition("docs-ordered");

        Assert.Equal("高优先级分组", definition.DisplayName);
        Assert.Equal(9, definition.Order);
    }

    /// <summary>
    /// 同 Order 下，先前只有分组键时由后来的特性补齐显示名，且不改 Order
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_WhenSameOrder_FillsMissingDisplayName()
    {
        var definition = GetSampleDefinition("docs-fill");

        Assert.Equal("补名分组", definition.DisplayName);
        Assert.Equal(2, definition.Order);
    }

    /// <summary>
    /// 方法级分组特性同样进入分组定义
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_ReadsMethodLevelGroup()
    {
        var definition = GetSampleDefinition("docs-method");

        Assert.Equal("方法级分组", definition.DisplayName);
        Assert.Equal(4, definition.Order);
    }

    /// <summary>
    /// 分组键忽略大小写归一，同名不同写法只产生一条定义
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_WhenGroupCaseDiffers_MergesIntoSingleDefinition()
    {
        var definition = GetSampleDefinition("docs-case");

        Assert.Equal("大写分组", definition.DisplayName);
        Assert.Equal(5, definition.Order);
    }

    /// <summary>
    /// IsEnabled=false 的分组不进文档
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_WhenDisabled_IsExcluded()
    {
        Assert.DoesNotContain(
            SwaggerInternals.GetGroupDefinitionsFromAttributes(),
            definition => string.Equals(definition.Group, "docs-disabled", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// VisibleInApiExplorer=false 的分组不进文档
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_WhenHiddenInApiExplorer_IsExcluded()
    {
        Assert.DoesNotContain(
            SwaggerInternals.GetGroupDefinitionsFromAttributes(),
            definition => string.Equals(definition.Group, "docs-hidden", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 纯空白分组键不产生分组定义
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_WhenGroupBlank_IsExcluded()
    {
        Assert.DoesNotContain(
            SwaggerInternals.GetGroupDefinitionsFromAttributes(),
            definition => string.IsNullOrWhiteSpace(definition.Group));
    }

    /// <summary>
    /// 分组定义按 Order 升序排列，Order 决定文档 UI 上的分组次序
    /// </summary>
    [Fact]
    public void GetGroupDefinitionsFromAttributes_OrdersByOrderAscending()
    {
        var groups = GetSampleGroups()
            .Select(definition => definition.Group)
            .ToArray();

        Assert.Equal(
            new[] { "docs-beta", "docs-fill", "docs-alpha", "docs-method", "DOCS-CASE", "docs-ordered" },
            groups);
    }

    /// <summary>
    /// 分组名列表按忽略大小写升序排列，与 Order 排序结果不同
    /// </summary>
    [Fact]
    public void GetGroupNamesFromAttributes_SortsCaseInsensitiveAscending()
    {
        var names = SwaggerInternals.GetGroupNamesFromAttributes()
            .Where(name => name.StartsWith("docs-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(), names);
        Assert.Contains("docs-alpha", names);
        Assert.Contains("docs-ordered", names);
    }

    /// <summary>
    /// 分组名列表与分组定义的键集合一一对应
    /// </summary>
    [Fact]
    public void GetGroupNamesFromAttributes_MatchesGroupDefinitionKeys()
    {
        var namesFromDefinitions = GetSampleGroups()
            .Select(definition => definition.Group)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var names = SwaggerInternals.GetGroupNamesFromAttributes()
            .Where(name => name.StartsWith("docs-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(namesFromDefinitions, names);
    }

    /// <summary>
    /// 取本测试工程贡献的分组定义（按 docs- 前缀过滤，排除框架自身可能引入的分组）
    /// </summary>
    /// <returns>分组定义列表</returns>
    private static IReadOnlyList<SwaggerInternals.DocGroupDefinition> GetSampleGroups()
    {
        return SwaggerInternals.GetGroupDefinitionsFromAttributes()
            .Where(definition => definition.Group.StartsWith("docs-", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// 按分组键取唯一一条分组定义
    /// </summary>
    /// <param name="group">分组键</param>
    /// <returns>分组定义</returns>
    private static SwaggerInternals.DocGroupDefinition GetSampleDefinition(string group)
    {
        return Assert.Single(
            GetSampleGroups(),
            definition => string.Equals(definition.Group, group, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 手写的 ApiExplorer 分组集合提供器
    /// </summary>
    private sealed class FakeApiDescriptionGroupCollectionProvider : IApiDescriptionGroupCollectionProvider
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="groupNames">分组名（允许 null 与空白，用于验证过滤）</param>
        public FakeApiDescriptionGroupCollectionProvider(params string?[] groupNames)
        {
            var groups = groupNames
                .Select(groupName => new ApiDescriptionGroup(groupName, new List<ApiDescription>()))
                .ToList();

            ApiDescriptionGroups = new ApiDescriptionGroupCollection(groups, 1);
        }

        /// <summary>
        /// 分组集合
        /// </summary>
        public ApiDescriptionGroupCollection ApiDescriptionGroups { get; }
    }
}
