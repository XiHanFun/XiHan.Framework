// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing;

namespace XiHan.Framework.Primitives.Tests.Identifiers;

/// <summary>
/// 回放 <c>operation-ids.json</c>：操作 ID 的文法与长度上限
/// </summary>
/// <remarks>
/// <c>spec/conventions/operation-ids.md</c>：整个 ID 不超过 128 个字符，以前只有 .NET 有这个上限（ADR-0276）
/// </remarks>
public sealed class OperationIdVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly OperationIdVectors _vectors = SpecVectors.Read<OperationIdVectors>("primitives", "operation-ids.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(OperationId.MaxLength, _vectors.MaxLength);

    [Fact]
    public void IsValid_AcceptsTheValidIds()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var id in _vectors.Valid)
        {
            Assert.True(OperationId.IsValid(id), id);
            Assert.Equal(id, OperationId.Validate(id));

            // 合法的 ID 恰好一个点：两段拼回去就是原文
            Assert.Equal(id, OperationId.ResourceOf(id) + "." + OperationId.VerbOf(id));
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidIds()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var id in _vectors.Invalid)
        {
            Assert.False(OperationId.IsValid(id), id);
            Assert.Throws<ArgumentException>(() => OperationId.Validate(id));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, id => id.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, id => id.Length == _vectors.MaxLength + 1 && id.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.'));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record OperationIdVectors(int MaxLength, string[] Valid, string[] Invalid);
}
