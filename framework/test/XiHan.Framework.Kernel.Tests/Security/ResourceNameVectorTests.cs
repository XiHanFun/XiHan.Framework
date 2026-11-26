// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Kernel.Tests.Security;

/// <summary>
/// 回放 <c>resource-names.json</c>：统一资源名的文法
/// </summary>
/// <remarks>
/// RFC-0008「资源名」：ID 段不含 <c>/</c>、空白与孤立的代理项（ADR-0321，向量里以 UTF-16 码元写出）。合法的解析之后再写出与原文相同，不合法的 <see cref="ResourceName.TryParse"/> 为假、<see cref="ResourceName.Parse"/> 抛出
/// </remarks>
public sealed class ResourceNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ResourceNameVectors _vectors = SpecVectors.Read<ResourceNameVectors>("security", "resource-names.json");

    [Fact]
    public void TheValidNames_ParseAndFormatBackToTheSameText()
    {
        Assert.NotEmpty(_vectors.Valid);
        Assert.All(_vectors.Valid, static entry =>
        {
            Assert.True(ResourceName.TryParse(entry.Input, out var name), entry.Name);
            Assert.Equal(entry.Input, name.ToString());
            Assert.Equal(name, ResourceName.Parse(entry.Input));
        });
    }

    [Fact]
    public void TheInvalidNames_AreRejected()
    {
        Assert.NotEmpty(_vectors.Invalid);
        Assert.All(_vectors.Invalid, static entry =>
        {
            Assert.False(ResourceName.TryParse(entry.Input, out _), entry.Name);
            Assert.Throws<ArgumentException>(() => ResourceName.Parse(entry.Input));
        });
    }

    [Fact]
    public void IdSegmentsWithLoneSurrogates_CannotBeConstructed_AndTheValidPairIsTheSameNameAsItsEscapedForm()
    {
        // 以码元写出的不合法样例都真有孤立的代理项；它们的 ID 段直接拿来构造同样被拒，解析与构造是同一条文法（ADR-0321）
        var lone = _vectors.Invalid.Where(static entry => entry.Utf16 is not null).ToList();
        Assert.NotEmpty(lone);
        Assert.All(lone, static entry =>
        {
            Assert.True(UnicodeText.HasLoneSurrogate(entry.Input), entry.Name);
            var id = entry.Input[(entry.Input.IndexOf('/', StringComparison.Ordinal) + 1)..];
            Assert.Throws<ArgumentException>(() => new ResourceName("a", "b", null, "c", id));
        });

        // 两个只差在孤立代理项上的 ID 宽松编码之后相同：改之前它们是两个合法的资源名，写进审计与 JSON 就分不开
        Assert.Equal(Encoding.UTF8.GetBytes("xrn:a:b::c/a\uD800"), Encoding.UTF8.GetBytes("xrn:a:b::c/a\uDBFF"));

        // 成对的代理项以码元写出与转义写出是同一个资源名，照常合法
        var byUnits = Assert.Single(_vectors.Valid, static entry => entry.Utf16 is not null);
        Assert.Contains(_vectors.Valid, entry => entry.Text == byUnits.Input);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Valid">必须接受的</param>
    /// <param name="Invalid">必须拒绝的</param>
    private sealed record ResourceNameVectors(ResourceNameCase[] Valid, ResourceNameCase[] Invalid);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Text">资源名的原文；JSON 字符串写不出的（孤立的代理项）为 <see langword="null"/></param>
    /// <param name="Utf16">以四位十六进制写出的 UTF-16 码元；写了原文的为 <see langword="null"/></param>
    private sealed record ResourceNameCase(string Name, string? Text = null, string[]? Utf16 = null)
    {
        /// <summary>
        /// 资源名的文本：原文，或由码元组成
        /// </summary>
        public string Input => SpecVectors.TextOf(Name, Text, Utf16);
    }
}
