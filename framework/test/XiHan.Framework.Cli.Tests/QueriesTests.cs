// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Cli.Knowledge;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 查询与渲染
/// </summary>
public class QueriesTests
{
    private static readonly FrameworkKnowledge Knowledge = FrameworkKnowledge.Load();

    [Theory]
    [InlineData("XiHan.Framework.Caching")]
    [InlineData("xihan.framework.caching")]
    [InlineData("Caching")]
    [InlineData("caching")]
    [InlineData("packages/caching")]
    [InlineData("XiHanCachingModule")]
    public void FindPackage_AcceptsEveryNameForm(string name)
    {
        Assert.Equal("XiHan.Framework.Caching", Queries.FindPackage(Knowledge, name)?.Id);
    }

    [Theory]
    [InlineData("web-api")]
    [InlineData("WebApi")]
    [InlineData("Web.Api")]
    public void FindPackage_IgnoresSeparators(string name)
    {
        Assert.Equal("XiHan.Framework.Web.Api", Queries.FindPackage(Knowledge, name)?.Id);
    }

    [Fact]
    public void RequirePackage_SuggestsNearNames()
    {
        var error = Assert.Throws<QueryException>(() => Queries.RequirePackage(Knowledge, "Cachng"));
        Assert.Contains("XiHan.Framework.Caching", error.Suggestions);
    }

    [Fact]
    public void ListPackages_OrdersLayersAlongDependencyDirection()
    {
        var layers = Queries.ListPackages(Knowledge).Select(g => g.Layer).ToList();
        Assert.True(layers.IndexOf("核心层") < layers.IndexOf("Web 层"));
        Assert.Equal(Knowledge.Packages.Count, Queries.ListPackages(Knowledge).Sum(g => g.Packages.Count));
        Assert.All(Queries.ListPackages(Knowledge, query: "Redis").SelectMany(g => g.Packages), p =>
            Assert.True($"{p.Id}{p.Description}{p.Summary}".Contains("Redis", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void RequirePage_AcceptsPathsTailsAndPackageNames()
    {
        Assert.Equal("guide/dynamic-api", Queries.RequirePage(Knowledge, "guide/dynamic-api").Path);
        Assert.Equal("guide/dynamic-api", Queries.RequirePage(Knowledge, "/guide/dynamic-api.md").Path);
        Assert.Equal("packages/caching", Queries.RequirePage(Knowledge, "XiHan.Framework.Caching").Path);
        Assert.Equal("quickstart", Queries.RequirePage(Knowledge, "quickstart").Path);
        Assert.Throws<QueryException>(() => Queries.RequirePage(Knowledge, "no-such-page"));
    }

    [Fact]
    public void Search_RanksTitleMatchesFirstAndRequiresAllTerms()
    {
        var results = Queries.Search(Knowledge, "分布式锁");
        Assert.Equal("guide/caching", results[0].Page.Path);
        Assert.DoesNotContain(results, r => r.Snippet.StartsWith('>'));
        Assert.Throws<QueryException>(() => Queries.Search(Knowledge, "分布式锁 不存在的词汇组合xyz"));
    }

    [Fact]
    public void Config_MatchesSectionOptionsClassAndPackage()
    {
        Assert.Equal(Knowledge.Sections.Count, Queries.Config(Knowledge).Count);
        Assert.Equal("XiHan:Caching:Redis", Assert.Single(Queries.Config(Knowledge, "XiHanRedisCacheOptions")).Section);
        Assert.All(Queries.Config(Knowledge, "Caching"), s => Assert.Equal("XiHan.Framework.Caching", s.Package));
        Assert.Throws<QueryException>(() => Queries.Config(Knowledge, "XiHan.Framework.Utils"));
    }

    [Fact]
    public void ConfigSections_RenderLiteralDefaultsAsJson()
    {
        var markdown = Rendering.ConfigSections(Queries.Config(Knowledge, "XiHan:Caching:Redis"), expand: true);
        Assert.Contains("| `IsEnabled` | `bool` | `false` |", markdown, StringComparison.Ordinal);
        Assert.Contains("\"Redis\": {", markdown, StringComparison.Ordinal);
        Assert.Contains("\"ConnectTimeout\": 5000", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\"InstanceName\"", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendering_UsesLineFeedOnly()
    {
        var caching = Queries.RequirePackage(Knowledge, "Caching");
        Assert.DoesNotContain('\r', Rendering.PackageInfo(Knowledge, caching));
        Assert.DoesNotContain('\r', Rendering.ConfigSections(Queries.Config(Knowledge, "Caching"), expand: true));
        Assert.DoesNotContain('\r', Queries.RequirePage(Knowledge, "guide/caching").Markdown);
    }

    [Fact]
    public void Changelog_PicksLatestSingleAndRange()
    {
        Assert.Equal([Knowledge.Changelog[0].Version], Queries.Changelog(Knowledge).Select(s => s.Version));
        var older = Knowledge.Changelog[2].Version;
        var range = Queries.Changelog(Knowledge, older, Knowledge.Changelog[0].Version);
        Assert.Equal(Knowledge.Changelog.Take(2).Select(s => s.Version), range.Select(s => s.Version));
        Assert.Throws<QueryException>(() => Queries.Changelog(Knowledge, "0.0.1"));
    }

    [Theory]
    [InlineData("4.6.0-preview.1", "4.6.0", -1)]
    [InlineData("4.6.2", "4.6.1", 1)]
    [InlineData("v4.6.2", "4.6.2", 0)]
    public void CompareVersions_OrdersPrereleaseBeforeRelease(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(Queries.CompareVersions(a, b)));
    }
}
