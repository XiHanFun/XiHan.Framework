// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Configuration.Abstractions.Tests;

/// <summary>
/// 回放 <c>secret-names.json</c>：密钥名的文法与长度上限
/// </summary>
/// <remarks>
/// <c>configuration/secret-names.json</c>（RFC-0014「密钥」，ADR-0277）。经 <see cref="SecretName.IsValid"/>、<see cref="SecretName.Validate"/> 与构造回放。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class SecretNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly SecretNameVectors _vectors = SpecVectors.Read<SecretNameVectors>("configuration", "secret-names.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(SecretName.MaxLength, _vectors.MaxLength);

    [Fact]
    public void IsValid_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var name in _vectors.Valid)
        {
            Assert.True(SecretName.IsValid(name), name);
            Assert.Equal(name, SecretName.Validate(name));
            Assert.Equal(name, new SecretName(name).Value);
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var name in _vectors.Invalid)
        {
            Assert.False(SecretName.IsValid(name), name);
            Assert.Throws<ArgumentException>(() => SecretName.Validate(name));
            Assert.Throws<ArgumentException>(() => new SecretName(name));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit_AndThePathRules()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, name => name.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MaxLength + 1 && name.All(char.IsAsciiLetterOrDigit));

        // 字符都在文法里、只因路径规则而不合法的也要有：含 ..、含 //、以 / 结尾
        Assert.Contains(_vectors.Invalid, static name => InAlphabet(name) && name.Contains("..", StringComparison.Ordinal) && !name.Contains("//", StringComparison.Ordinal) && !name.EndsWith('/'));
        Assert.Contains(_vectors.Invalid, static name => InAlphabet(name) && name.Contains("//", StringComparison.Ordinal) && !name.Contains("..", StringComparison.Ordinal) && !name.EndsWith('/'));
        Assert.Contains(_vectors.Invalid, static name => InAlphabet(name) && name.EndsWith('/') && !name.Contains("..", StringComparison.Ordinal) && !name.Contains("//", StringComparison.Ordinal));

        // 区分大小写：含大写的名字合文法
        Assert.Contains(_vectors.Valid, static name => name.Any(char.IsAsciiLetterUpper));
    }

    /// <summary>
    /// 名字是否以字母或数字开头、只含文法里的字符、不超过上限
    /// </summary>
    /// <param name="name">名字</param>
    private static bool InAlphabet(string name)
        => name.Length is >= 1 and <= SecretName.MaxLength && char.IsAsciiLetterOrDigit(name[0]) && name.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or '-');

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">密钥名的字符数上限</param>
    /// <param name="Valid">合文法的密钥名</param>
    /// <param name="Invalid">不合文法的密钥名</param>
    private sealed record SecretNameVectors(int MaxLength, string[] Valid, string[] Invalid);
}
