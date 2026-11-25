// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>container-names.json</c>：容器名的文法与长度上下限
/// </summary>
/// <remarks>
/// 规范 RFC-0012「标识」：3 到 63 个小写字母、数字与连字符，首尾是字母或数字，按整串匹配（ADR-0277）
/// </remarks>
public sealed class ContainerNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ContainerNameVectors _vectors = SpecVectors.Read<ContainerNameVectors>("objectstorage", "container-names.json");

    [Fact]
    public void IsValid_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var name in _vectors.Valid)
        {
            Assert.True(ContainerName.IsValid(name), name);
            Assert.Equal(name, ContainerName.Validate(name));
            Assert.Equal(name, new ContainerName(name).Value);
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var name in _vectors.Invalid)
        {
            Assert.False(ContainerName.IsValid(name), name);
            Assert.Throws<ArgumentException>(() => ContainerName.Validate(name));
            Assert.Throws<ArgumentException>(() => new ContainerName(name));
        }
    }

    [Fact]
    public void TheLengthLimits_AreTheOnesInTheVectors()
    {
        // 容器名没有长度常量，上下限写在正则里：用只差长度的名字核对两头
        Assert.True(ContainerName.IsValid(new string('a', _vectors.MinLength)));
        Assert.False(ContainerName.IsValid(new string('a', _vectors.MinLength - 1)));
        Assert.True(ContainerName.IsValid(new string('a', _vectors.MaxLength)));
        Assert.False(ContainerName.IsValid(new string('a', _vectors.MaxLength + 1)));
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimits()
    {
        // 防止向量换了写法后上下限一条都没核对还照样通过：正好到限的要收，只因长度而不合法的要拒
        Assert.Contains(_vectors.Valid, name => name.Length == _vectors.MinLength);
        Assert.Contains(_vectors.Valid, name => name.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MinLength - 1 && IsWellFormedApartFromLength(name));
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MaxLength + 1 && IsWellFormedApartFromLength(name));
    }

    /// <summary>
    /// 除长度外是否合文法：只含小写字母、数字与连字符，首尾是字母或数字
    /// </summary>
    private static bool IsWellFormedApartFromLength(string name)
        => name.Length > 0
            && name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            && name[0] != '-'
            && name[^1] != '-';

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MinLength">长度下限</param>
    /// <param name="MaxLength">长度上限</param>
    /// <param name="Valid">合法的容器名</param>
    /// <param name="Invalid">不合法的容器名</param>
    private sealed record ContainerNameVectors(int MinLength, int MaxLength, string[] Valid, string[] Invalid);
}
