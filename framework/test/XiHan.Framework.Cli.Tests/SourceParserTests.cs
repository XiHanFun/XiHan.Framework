// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Cli.Knowledge;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 项目文件、模块类与选项类的解析
/// </summary>
public class SourceParserTests
{
    [Fact]
    public void ParseProject_SplitsFrameworkAndThirdPartyReferences()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><Description>示例库</Description></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="StackExchange.Redis" Version="2.9.0" />
                <ProjectReference Include="..\XiHan.Framework.Uow\XiHan.Framework.Uow.csproj" />
                <ProjectReference Include="..\Other\Other.csproj" />
              </ItemGroup>
            </Project>
            """;
        var (description, framework, thirdParty) = SourceParser.ParseProject(xml);
        Assert.Equal("示例库", description);
        Assert.Equal(["XiHan.Framework.Uow"], framework);
        Assert.Equal([new PackageDependency("StackExchange.Redis", "2.9.0")], thirdParty);
    }

    [Fact]
    public void ParseModules_ReadsDependsOnAndBareModules()
    {
        const string source = """
            [DependsOn(
                typeof(XiHanUowModule),
                typeof(Foo.XiHanThreadingModule)
                )]
            public class XiHanSampleModule : XiHanModule
            {
            }

            public class XiHanBareModule : XiHanModule;
            """;
        var modules = SourceParser.ParseModules(source);
        Assert.Equal(2, modules.Count);
        Assert.Equal(["XiHanUowModule", "Foo.XiHanThreadingModule"], modules[0].DependsOn);
        Assert.Equal("XiHanBareModule", modules[1].Name);
        Assert.Empty(modules[1].DependsOn);
    }

    [Fact]
    public void ParseOptions_ReadsPropertiesDefaultsDocsAndNestedTypes()
    {
        const string source = """
            namespace Sample;

            /// <summary>
            /// 示例选项，见 <see cref="T:Sample.Other"/>
            /// </summary>
            public class SampleOptions
            {
                /// <summary>
                /// 配置节名称
                /// </summary>
                public const string SectionName = "XiHan:Sample";

                /// <summary>
                /// 是否启用
                /// </summary>
                [Required]
                public bool IsEnabled { get; set; } = true;

                /// <summary>
                /// 地址
                /// </summary>
                /// <remarks>如 <c>localhost</c></remarks>
                public string? Host { get; init; }

                /// <summary>
                /// 名单
                /// </summary>
                public List<string> Names { get; set; } = [
                    "a",
                ];

                /// <summary>
                /// 子项
                /// </summary>
                public Dictionary<string, SampleItem> Items { get; set; } = new();

                public string Computed => Host ?? string.Empty;

                public void Method()
                {
                    var local = 1;
                }
            }

            /// <summary>
            /// 子项选项
            /// </summary>
            public class SampleItem
            {
                /// <summary>
                /// 权重
                /// </summary>
                public int Weight { get; set; } = 1;
            }
            """;
        var parsed = SourceParser.ParseOptions(source);
        Assert.NotNull(parsed);
        var (section, options, others) = parsed.Value;
        Assert.Equal("XiHan:Sample", section);
        Assert.Equal("SampleOptions", options.Name);
        Assert.Equal("示例选项，见 Other", options.Summary);
        Assert.Equal(["IsEnabled", "Host", "Names", "Items"], options.Properties.Select(p => p.Name));
        Assert.Equal("true", options.Properties[0].Default);
        Assert.Equal("是否启用", options.Properties[0].Summary);
        Assert.Equal("string?", options.Properties[1].Type);
        Assert.Null(options.Properties[1].Default);
        Assert.Equal("地址 如 localhost", options.Properties[1].Summary);
        Assert.Equal("[ …", options.Properties[2].Default);
        Assert.Equal("Dictionary<string, SampleItem>", options.Properties[3].Type);
        var item = Assert.Single(others);
        Assert.Equal("SampleItem", item.Name);
        Assert.Equal("1", Assert.Single(item.Properties).Default);
    }

    [Fact]
    public void ParseOptions_ReturnsNullWithoutSectionName()
    {
        Assert.Null(SourceParser.ParseOptions("public class Plain { public int X { get; set; } }"));
    }

    [Fact]
    public void MarkdownText_TitleSummaryAndFields()
    {
        const string markdown = """
            # XiHan.Framework.Sample

            > 示例包：做一件事。第二句不要。

            - **NuGet**：`XiHan.Framework.Sample`
            - **所在层**：基础设施层

            ```csharp
            // 代码块里的段落不算摘要。
            ```
            """;
        Assert.Equal("XiHan.Framework.Sample", MarkdownText.Title(markdown));
        Assert.Equal("示例包：做一件事。", MarkdownText.Summary(markdown));
        Assert.Equal("XiHan.Framework.Sample", MarkdownText.Field(markdown, "NuGet"));
        Assert.Equal("基础设施层", MarkdownText.Field(markdown, "所在层"));
        Assert.Null(MarkdownText.Field(markdown, "模块类"));
    }
}
