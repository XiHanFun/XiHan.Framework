// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using XiHan.Framework.Cli.Knowledge;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 嵌入的知识库与仓库源码一致
/// </summary>
public class KnowledgeTests
{
    private static readonly FrameworkKnowledge Knowledge = FrameworkKnowledge.Load();

    private static string FrameworkRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("找不到 framework 目录");
    }

    [Fact]
    public void Version_MatchesVersionProps()
    {
        var props = File.ReadAllText(Path.Combine(FrameworkRoot(), "props", "version.props"));
        var version = Regex.Match(props, "<Version>([^<]+)</Version>").Groups[1].Value;
        Assert.Equal(version, Knowledge.Version);
    }

    [Fact]
    public void Packages_CoverEverySourceProject()
    {
        var projects = Directory.GetDirectories(Path.Combine(FrameworkRoot(), "src"))
            .Where(directory => File.Exists(Path.Combine(directory, $"{Path.GetFileName(directory)}.csproj")))
            .Select(directory => Path.GetFileName(directory))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(projects, Knowledge.Packages.Select(p => p.Id).ToList());
    }

    [Fact]
    public void Packages_AllHaveReferencePageAndLayer()
    {
        var missing = Knowledge.Packages.Where(p => p.DocPath is null || p.Layer is null).Select(p => p.Id).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void ConfigSections_CoverEverySectionNameInSource()
    {
        var sections = Directory.EnumerateFiles(Path.Combine(FrameworkRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "const\\s+string\\s+SectionName\\s*=\\s*\"([^\"]+)\"").Select(m => m.Groups[1].Value))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(sections, Knowledge.Sections.Select(s => s.Section).ToList());
    }

    [Fact]
    public void Caching_ModuleDependenciesAndRedisSection()
    {
        var caching = Knowledge.Packages.Single(p => p.Id == "XiHan.Framework.Caching");
        var module = Assert.Single(caching.Modules);
        Assert.Equal("XiHanCachingModule", module.Name);
        Assert.Contains("XiHanUowModule", module.DependsOn);
        Assert.Contains("XiHan.Framework.Uow", caching.FrameworkDependencies);

        var redis = Knowledge.Sections.Single(s => s.Section == "XiHan:Caching:Redis");
        Assert.Equal("XiHanRedisCacheOptions", redis.Options.Name);
        var enabled = redis.Options.Properties.Single(p => p.Name == "IsEnabled");
        Assert.Equal("bool", enabled.Type);
        Assert.Equal("false", enabled.Default);
        Assert.False(string.IsNullOrWhiteSpace(enabled.Summary));
    }

    [Fact]
    public void Pages_IncludeGuidesAndPackagesButNotChangelog()
    {
        Assert.Contains(Knowledge.Pages, p => p.Path == "guide/dynamic-api" && p.Title == "动态 API");
        Assert.Contains(Knowledge.Pages, p => p.Path == "packages/caching");
        Assert.DoesNotContain(Knowledge.Pages, p => p.Path == "changelog");
        Assert.NotEmpty(Knowledge.Changelog);
        Assert.Equal(Knowledge.Version, Knowledge.Changelog[0].Version);
    }

    [Fact]
    public void Skill_IsEmbedded()
    {
        Assert.StartsWith("---\nname: xihan-framework", Knowledge.Skill.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }
}
