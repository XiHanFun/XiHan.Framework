// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 提示词模板的渲染与目录的合并规则。
/// </summary>
public sealed class AIPromptTests
{
    [Fact]
    public void Render_ReplacesPlaceholders_EscapesBraces_AndReportsMissingArguments()
    {
        var template = new AIPromptTemplate("assistant", "You are {name}, version {version}. Use {{braces}} literally. {name} again.", "desc");
        Assert.Equal(["name", "version"], template.Placeholders);
        Assert.Equal("You are Echo, version 2. Use {braces} literally. Echo again.", template.Render(MessageArguments.Of(("name", "Echo"), ("version", 2))).ValueOrThrow());
        Assert.Equal("", new AIPromptTemplate("empty", "").Render().ValueOrThrow());
        Assert.Equal("no placeholders }} {{", new AIPromptTemplate("plain", "no placeholders }}}} {{{{").Render().ValueOrThrow());
        Assert.Equal("open {brace", new AIPromptTemplate("open", "open {brace").Render().ValueOrThrow());

        var missing = template.Render(MessageArguments.Of(("name", "Echo")));
        Assert.Equal(AIErrors.RequestInvalid, missing.Error?.Code);
        Assert.Equal("version", missing.Error!.Details!["argument"]);
        Assert.Equal("", new AIPromptTemplate("nullable", "{x}").Render(MessageArguments.Of(("x", null))).ValueOrThrow());
        Assert.ThrowsAny<ArgumentException>(() => new AIPromptTemplate("Bad Name", "x"));
    }

    [Fact]
    public void Catalog_MergesConfigurationAndExplicitRegistrations()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:AI:Prompts:assistant:Template"] = "from config {name}",
            ["XiHan:AI:Prompts:assistant:Description"] = "configured",
            ["XiHan:AI:Prompts:greeting"] = "hello {name}",
        }).Build();
        var options = AIOptions.FromConfiguration(configuration);
        Assert.Equal(["assistant", "greeting"], options.Prompts.Select(static prompt => prompt.Name));

        var catalog = new AIPromptCatalog(options, [new AIPromptTemplate("assistant", "explicit {name}"), new AIPromptTemplate("zzz", "last")]);
        Assert.Equal(["assistant", "greeting", "zzz"], catalog.All().Select(static prompt => prompt.Name));
        Assert.Equal("explicit {name}", catalog.Find("assistant").ValueOrThrow().Template);
        Assert.Equal("hello {name}", catalog.Find("greeting").ValueOrThrow().Template);
        Assert.False(catalog.Find("missing").HasValue);
    }
}
