// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 把查询结果渲染成 Markdown，命令行与 MCP 工具输出同一份文本
/// </summary>
internal static class Rendering
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true, NewLine = "\n", Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 按层列出的包
    /// </summary>
    public static string PackageList(FrameworkKnowledge knowledge, IReadOnlyList<(string Layer, IReadOnlyList<PackageInfo> Packages)> groups)
    {
        var builder = new StringBuilder();
        builder.Line($"# XiHan.Framework 包 · v{knowledge.Version}").Line();
        builder.Line("查看某个包用 info，完整文档用 doc，配置节用 config。").Line();
        foreach (var (layer, packages) in groups)
        {
            builder.Line($"## {layer}").Line();
            builder.Line("| 包 | 模块类 | 说明 |").Line("| --- | --- | --- |");
            foreach (var package in packages)
            {
                var modules = package.Modules.Count == 0 ? "—" : string.Join(' ', package.Modules.Select(m => Code(m.Name)));
                builder.Line($"| {Code(package.Id)} | {modules} | {Cell(package.Summary ?? package.Description)} |");
            }

            builder.Line();
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 一个包的概况：安装、模块依赖、包依赖与配置节
    /// </summary>
    public static string PackageInfo(FrameworkKnowledge knowledge, PackageInfo package)
    {
        var builder = new StringBuilder();
        builder.Line($"# {package.Id}").Line();
        if (!string.IsNullOrWhiteSpace(package.Summary))
        {
            builder.Line(package.Summary).Line();
        }

        builder.Line($"- **说明**：{package.Description}");
        if (package.Layer is not null)
        {
            builder.Line($"- **所在层**：{package.Layer}");
        }

        if (package.DocPath is not null)
        {
            builder.Line($"- **文档**：{FrameworkKnowledge.Site}/{package.DocPath}");
        }

        builder.Line();
        builder.Line("## 安装与启用").Line();
        builder.Line("```bash").Line($"dotnet add package {package.Id} --version {knowledge.Version}").Line("```").Line();
        if (package.Modules.Count > 0)
        {
            builder.Line("```csharp");
            builder.Line($"[DependsOn(typeof({package.Modules[0].Name}))]");
            builder.Line("public class MyModule : XiHanModule;");
            builder.Line("```").Line();
            builder.Line("## 模块依赖").Line();
            foreach (var module in package.Modules)
            {
                builder.Line(module.DependsOn.Count == 0
                    ? $"- {Code(module.Name)}：没有 [DependsOn]"
                    : $"- {Code(module.Name)} → {string.Join(" · ", module.DependsOn.Select(Code))}");
            }

            builder.Line();
        }
        else
        {
            builder.Line("本包没有模块类，直接引用其中的类型即可。").Line();
        }

        builder.Line("## 包依赖").Line();
        builder.Line($"- **框架包**：{(package.FrameworkDependencies.Count == 0 ? "无" : string.Join(" · ", package.FrameworkDependencies.Select(Code)))}");
        builder.Line($"- **第三方包**：{(package.ThirdPartyDependencies.Count == 0 ? "无" : string.Join(" · ", package.ThirdPartyDependencies.Select(d => Code(d.Version.Length == 0 ? d.Name : $"{d.Name} {d.Version}"))))}");
        builder.Line();

        var sections = knowledge.Sections.Where(s => s.Package == package.Id).ToList();
        if (sections.Count > 0)
        {
            builder.Line("## 配置节").Line();
            foreach (var section in sections)
            {
                builder.Line($"- {Code(section.Section)} → {Code(section.Options.Name)}{(section.Options.Summary.Length > 0 ? $"：{section.Options.Summary}" : string.Empty)}");
            }

            builder.Line().Line("逐项说明用 config 查询。");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 文档页列表
    /// </summary>
    public static string PageList(FrameworkKnowledge knowledge)
    {
        var builder = new StringBuilder();
        builder.Line($"# XiHan.Framework 文档页 · v{knowledge.Version}").Line();
        builder.Line("用站内路径读取全文；包的参考页也可以直接写包名。").Line();
        builder.Line("| 路径 | 标题 | 摘要 |").Line("| --- | --- | --- |");
        foreach (var page in knowledge.Pages.OrderBy(p => p.Path, StringComparer.Ordinal))
        {
            builder.Line($"| {Code(page.Path)} | {Cell(page.Title)} | {Cell(page.Summary)} |");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 检索结果
    /// </summary>
    public static string SearchResults(string query, IReadOnlyList<(DocPage Page, int Score, string Snippet)> results)
    {
        var builder = new StringBuilder();
        builder.Line($"# 检索：{query}").Line();
        builder.Line("按相关度排序，用 doc 读取全文。").Line();
        foreach (var (page, _, snippet) in results)
        {
            builder.Line($"- {Code(page.Path)} {page.Title}{(page.Summary.Length > 0 ? $"：{page.Summary}" : string.Empty)}");
            if (snippet.Length > 0)
            {
                builder.Line($"  > {snippet}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 配置节：只列出时给总表，具体查询时逐项展开并附配置片段
    /// </summary>
    public static string ConfigSections(IReadOnlyList<ConfigSection> sections, bool expand)
    {
        var builder = new StringBuilder();
        if (!expand)
        {
            builder.Line("# 配置节").Line();
            builder.Line("写在 appsettings.json 里，路径按冒号分层。查看某一节的全部属性：config <配置节或包名>。").Line();
            builder.Line("| 配置节 | 选项类 | 包 | 说明 |").Line("| --- | --- | --- | --- |");
            foreach (var section in sections)
            {
                builder.Line($"| {Code(section.Section)} | {Code(section.Options.Name)} | {Code(section.Package)} | {Cell(section.Options.Summary)} |");
            }

            return builder.ToString().TrimEnd();
        }

        foreach (var section in sections)
        {
            builder.Line($"# {section.Section}").Line();
            builder.Line($"选项类 {Code(section.Options.Name)}，所在包 {Code(section.Package)}。{section.Options.Summary}").Line();
            AppendProperties(builder, section.Options);
            foreach (var nested in section.NestedTypes)
            {
                builder.Line($"## {nested.Name}").Line();
                if (nested.Summary.Length > 0)
                {
                    builder.Line(nested.Summary).Line();
                }

                AppendProperties(builder, nested);
            }

            var sample = Sample(section);
            if (sample is not null)
            {
                builder.Line("按缺省值写出的配置片段（只含字面量缺省值的项）：").Line();
                builder.Line("```json").Line(sample).Line("```").Line();
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 更新日志
    /// </summary>
    public static string Changelog(IReadOnlyList<ChangelogSection> sections)
    {
        return sections.Count == 0
            ? "没有匹配的更新条目。"
            : string.Join("\n\n", sections.Select(s => $"## v{s.Version}{(s.Date.Length > 0 ? $" ({s.Date})" : string.Empty)}\n\n{s.Body}"));
    }

    private static void AppendProperties(StringBuilder builder, OptionType type)
    {
        if (type.Properties.Count == 0)
        {
            builder.Line("没有可配置的公开属性。").Line();
            return;
        }

        builder.Line("| 属性 | 类型 | 缺省值 | 说明 |").Line("| --- | --- | --- | --- |");
        foreach (var property in type.Properties)
        {
            builder.Line($"| {Code(property.Name)} | {Cell(Code(property.Type))} | {(property.Default is null ? "—" : Cell(Code(property.Default)))} | {Cell(property.Summary)} |");
        }

        builder.Line();
    }

    private static string? Sample(ConfigSection section)
    {
        var values = new JsonObject();
        foreach (var property in section.Options.Properties)
        {
            if (Literal(property.Default) is { } node)
            {
                values[property.Name] = node;
            }
        }

        if (values.Count == 0)
        {
            return null;
        }

        JsonNode root = values;
        foreach (var segment in section.Section.Split(':').Reverse())
        {
            root = new JsonObject { [segment] = root };
        }

        return root.ToJsonString(IndentedJson);
    }

    private static JsonNode? Literal(string? expression)
    {
        if (expression is null)
        {
            return null;
        }

        var value = expression.Trim();
        if (value is "true" or "false")
        {
            return JsonValue.Create(value == "true");
        }

        if (value is "string.Empty" or "\"\"")
        {
            return JsonValue.Create(string.Empty);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"' && !value[1..^1].Contains('"'))
        {
            return JsonValue.Create(value[1..^1]);
        }

        var number = value.TrimEnd('L', 'l', 'd', 'D', 'f', 'F', 'm', 'M').Replace("_", string.Empty, StringComparison.Ordinal);
        if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }

        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? JsonValue.Create(real) : null;
    }

    private static StringBuilder Line(this StringBuilder builder, string text = "")
    {
        return builder.Append(text).Append('\n');
    }

    private static string Code(string value) => $"`{value.Replace("`", string.Empty, StringComparison.Ordinal)}`";

    private static string Cell(string value) => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
}
