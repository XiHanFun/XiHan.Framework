// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 回放 <c>ai-names.json</c>：AI 子系统里名字的文法
/// </summary>
/// <remarks>
/// 规范 RFC-0022「Provider 目录」（ADR-0277）：Provider 名、提示词名与知识库的集合名用同一条文法，按整串匹配，结尾的换行不例外；写在配置里的不合法名字是启动失败。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class AINameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly NameVectors _vectors = SpecVectors.Read<NameVectors>("ai", "ai-names.json");

    [Fact]
    public void MaxNameLength_IsTheOneInTheVectors()
        => Assert.Equal(AIIdentifiers.MaxNameLength, _vectors.MaxLength);

    [Fact]
    public void IsValidName_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var name in _vectors.Valid)
        {
            Assert.True(AIIdentifiers.IsValidName(name), name);
            Assert.Equal(name, AIIdentifiers.ValidateName(name, nameof(name)));

            // 用这条文法的三处：Provider 名、提示词名、知识库的集合名（赋值与读配置）
            Assert.Equal(name, new AIProviderOptions(name, "fake").Name);
            Assert.Equal(name, new AIPromptTemplate(name, string.Empty).Name);
            Assert.Equal(name, new KnowledgeOptions { Collection = name }.Collection);
            Assert.Equal(name, AIOptions.FromConfiguration(CollectionConfiguration(name)).Knowledge.Collection);
        }
    }

    [Fact]
    public void IsValidName_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var name in _vectors.Invalid)
        {
            Assert.False(AIIdentifiers.IsValidName(name), name);
            Assert.Throws<ArgumentException>(() => AIIdentifiers.ValidateName(name, nameof(name)));
            Assert.Throws<ArgumentException>(() => new AIProviderOptions(name, "fake"));
            Assert.Throws<ArgumentException>(() => new AIPromptTemplate(name, string.Empty));
            Assert.Throws<ArgumentException>(() => new KnowledgeOptions { Collection = name });
            Assert.Throws<ArgumentException>(() => AIOptions.FromConfiguration(CollectionConfiguration(name)));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒，结尾带换行的要拒
        Assert.Contains(_vectors.Valid, name => name.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MaxLength + 1 && name.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)));
        Assert.Contains(_vectors.Invalid, static name => name.EndsWith('\n'));
    }

    /// <summary>
    /// 把名字写在 <c>XiHan:AI:Knowledge:Collection</c> 的配置
    /// </summary>
    private static IConfigurationRoot CollectionConfiguration(string name)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [$"{AIOptions.ConfigurationSection}:Knowledge:Collection"] = name })
            .Build();

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">名字最长的字符数</param>
    /// <param name="Valid">必须接受的</param>
    /// <param name="Invalid">必须拒绝的</param>
    private sealed record NameVectors(int MaxLength, string[] Valid, string[] Invalid);
}
