// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 回放 <c>index-names.json</c>：索引名的推算、长度上限与显式的名字
/// </summary>
/// <remarks>
/// 规范 RFC-0005「索引声明」：前缀加表名、再加每列一段缩写，以 <c>_</c> 连接；超过 63 个字符是声明错误、不截断；声明里给了名字时用它、不推（ADR-0072、ADR-0249、ADR-0277）。推算与显式名字的文法各有内部入口，这里直接回放；长度上限由 <see cref="EntityIndexes.Resolve"/> 对两种名字一并核对（<see cref="EntityIndexesTests"/> 覆盖它的拒绝），这里按同一个常量 <see cref="EntityIndexes.MaxNameLength"/> 判断
/// </remarks>
public sealed class EntityIndexNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly IndexNameVectors _vectors = SpecVectors.Read<IndexNameVectors>("persistence", "index-names.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(EntityIndexes.MaxNameLength, _vectors.MaxLength);

    [Fact]
    public void NameOf_AbbreviatesEveryProperty()
    {
        Assert.NotEmpty(_vectors.Abbreviations);
        foreach (var vector in _vectors.Abbreviations)
        {
            // 缩写没有单独的入口：只有一列的索引名就是前缀、表名与这一列的缩写
            Assert.Equal($"{_vectors.Prefix}t_{vector.Abbreviation}", EntityIndexes.NameOf("t", [vector.Property], isUnique: false));
            Assert.Equal($"{_vectors.UniquePrefix}t_{vector.Abbreviation}", EntityIndexes.NameOf("t", [vector.Property], isUnique: true));
        }
    }

    [Fact]
    public void NameOf_DerivesTheNameOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Names);
        var failures = new List<string>();
        foreach (var vector in _vectors.Names)
        {
            // 超长是声明错误、不截断：向量里这时没有名字
            var derived = EntityIndexes.NameOf(vector.Table, vector.Properties, vector.Unique);
            var accepted = derived.Length <= EntityIndexes.MaxNameLength ? derived : null;
            if (accepted != vector.Index)
            {
                failures.Add($"{vector.Name}: derived '{derived}' ({derived.Length} characters), the vector has '{vector.Index ?? "none"}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ExplicitName_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.ExplicitNames.Valid);
        Assert.All(_vectors.ExplicitNames.Valid, static name => Assert.True(Accepts(name), name));
    }

    [Fact]
    public void ExplicitName_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.ExplicitNames.Invalid);
        Assert.All(_vectors.ExplicitNames.Invalid, static name => Assert.False(Accepts(name), name));
    }

    [Fact]
    public void TheVectors_CoverBothPrefixesAndBothSidesOfTheLimit()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过
        // 两个前缀都要有推得出名字的用例
        Assert.Contains(_vectors.Names, vector => !vector.Unique && vector.Index is { } index && index.StartsWith(_vectors.Prefix, StringComparison.Ordinal));
        Assert.Contains(_vectors.Names, vector => vector.Unique && vector.Index is { } index && index.StartsWith(_vectors.UniquePrefix, StringComparison.Ordinal));

        // 推出的名字：正好到上限的要有名字，多一个字符的没有
        Assert.Contains(_vectors.Names, vector => vector.Index?.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Names, vector => vector.Index is null && EntityIndexes.NameOf(vector.Table, vector.Properties, vector.Unique).Length == _vectors.MaxLength + 1);

        // 显式的名字：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.ExplicitNames.Valid, name => name.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.ExplicitNames.Invalid, name => name.Length == _vectors.MaxLength + 1 && char.IsAsciiLetter(name[0]) && name.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_'));

        // 缩写：连续大写里后跟小写的大写字母开新词、下划线只分词
        Assert.Contains(_vectors.Abbreviations, static vector => vector.Property.Contains('_', StringComparison.Ordinal));
        Assert.Contains(_vectors.Abbreviations, static vector => vector.Property.Length > 2 && char.IsUpper(vector.Property[0]) && char.IsUpper(vector.Property[1]) && vector.Property.Any(char.IsLower));
    }

    /// <summary>
    /// 显式的名字是否被接受
    /// </summary>
    /// <remarks>
    /// 文法由 <see cref="EntityIndexes.ExplicitName"/> 核对，不合文法即抛出；长度上限与推出的名字是同一条
    /// </remarks>
    private static bool Accepts(string name)
    {
        var error = Record.Exception(() => EntityIndexes.ExplicitName(typeof(EntityIndexNameVectorTests), name));
        Assert.True(error is null or InvalidOperationException, $"'{name}' threw '{error?.GetType().Name}' instead of 'InvalidOperationException'.");
        return error is null && name.Length <= EntityIndexes.MaxNameLength;
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">索引名的长度上限</param>
    /// <param name="Prefix">普通索引的前缀</param>
    /// <param name="UniquePrefix">唯一索引的前缀</param>
    /// <param name="Abbreviations">属性名的缩写</param>
    /// <param name="Names">推出的名字</param>
    /// <param name="ExplicitNames">显式的名字</param>
    private sealed record IndexNameVectors(int MaxLength, string Prefix, string UniquePrefix, AbbreviationCase[] Abbreviations, IndexNameCase[] Names, ExplicitNameCases ExplicitNames);

    /// <summary>
    /// 一个属性名与它的缩写
    /// </summary>
    /// <param name="Property">属性名</param>
    /// <param name="Abbreviation">缩写</param>
    private sealed record AbbreviationCase(string Property, string Abbreviation);

    /// <summary>
    /// 一个索引声明与推出的名字
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Table">Provider 映射出的表名</param>
    /// <param name="Properties">声明的列，按声明的次序</param>
    /// <param name="Unique">是不是唯一索引</param>
    /// <param name="Index">推出的名字；<see langword="null"/> 表示超长，是声明错误</param>
    private sealed record IndexNameCase(string Name, string Table, string[] Properties, bool Unique, string? Index);

    /// <summary>
    /// 显式名字的用例
    /// </summary>
    /// <param name="Valid">合法的</param>
    /// <param name="Invalid">不合法的</param>
    private sealed record ExplicitNameCases(string[] Valid, string[] Invalid);
}
