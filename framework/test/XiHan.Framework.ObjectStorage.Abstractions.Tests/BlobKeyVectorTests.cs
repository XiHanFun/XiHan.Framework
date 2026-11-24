// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>blob-keys.json</c>：对象键的文法、长度上限与保留前缀
/// </summary>
/// <remarks>
/// 规范 RFC-0012「标识」：<c>/</c> 分段的路径，每段非空、不是 <c>.</c> 或 <c>..</c>，不含空白、控制字符与反斜线；首段是保留段的键留给框架自己的对象，不在键文法之内（ADR-0090、ADR-0277）
/// </remarks>
public sealed class BlobKeyVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BlobKeyVectors _vectors = ConformanceVectors.Load<BlobKeyVectors>("blob-keys.json");

    [Fact]
    public void TheConstants_AreTheOnesInTheVectors()
    {
        Assert.Equal(BlobKey.MaxLength, _vectors.MaxLength);
        Assert.Equal(BlobKey.Separator.ToString(), _vectors.Separator);
        Assert.Equal(BlobKey.ReservedSegment, _vectors.ReservedSegment);
    }

    [Fact]
    public void IsValid_AcceptsTheValidKeys()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var vector in _vectors.Valid)
        {
            Assert.True(BlobKey.IsValid(vector.Key), vector.Name);
            Assert.False(BlobKey.IsReserved(vector.Key), vector.Name);
            Assert.Equal(vector.Key, BlobKey.Validate(vector.Key));
            Assert.Equal(vector.Key, new BlobKey(vector.Key).Value);
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidKeys_AndNoneIsReserved()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var vector in _vectors.Invalid)
        {
            Assert.False(BlobKey.IsValid(vector.Key), vector.Name);
            Assert.False(BlobKey.IsReserved(vector.Key), vector.Name);
            Assert.Throws<ArgumentException>(() => BlobKey.Validate(vector.Key));
            Assert.Throws<ArgumentException>(() => new BlobKey(vector.Key));
        }
    }

    [Fact]
    public void IsReserved_RecognizesTheReservedKeys_AndTheyAreNotValid()
    {
        Assert.NotEmpty(_vectors.Reserved);
        foreach (var vector in _vectors.Reserved)
        {
            Assert.True(BlobKey.IsReserved(vector.Key), vector.Name);
            Assert.False(BlobKey.IsValid(vector.Key), vector.Name);
            Assert.Throws<ArgumentException>(() => BlobKey.Validate(vector.Key));
            Assert.Throws<ArgumentException>(() => new BlobKey(vector.Key));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit_AndTheEdgesOfTheReservedPrefix()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, vector => vector.Key.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, vector => vector.Key.Length == _vectors.MaxLength + 1 && IsWellFormedApartFromLength(vector.Key));

        // 保留前缀只看首段、区分大小写：保留段本身与它下面的键是保留的，只是像它的键照常合法
        var under = _vectors.ReservedSegment + _vectors.Separator;
        Assert.Contains(_vectors.Reserved, vector => vector.Key == _vectors.ReservedSegment);
        Assert.Contains(_vectors.Reserved, vector => vector.Key.StartsWith(under, StringComparison.Ordinal));
        Assert.Contains(_vectors.Valid, vector => vector.Key.StartsWith(_vectors.ReservedSegment, StringComparison.Ordinal));
        Assert.Contains(_vectors.Valid, vector => vector.Key.StartsWith(under, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_vectors.Valid, vector => vector.Key.Contains(_vectors.Separator + under, StringComparison.Ordinal));
    }

    /// <summary>
    /// 除长度外是否合文法
    /// </summary>
    /// <remarks>
    /// 只认向量里用来卡长度的那种键，不是完整的键文法：各段非空、不是 <c>.</c> 或 <c>..</c>，只含 ASCII 字母、数字与点
    /// </remarks>
    private static bool IsWellFormedApartFromLength(string key)
        => key.Split(_vectors.Separator).All(static segment => segment.Length > 0 && segment is not ("." or "..") && segment.All(static c => char.IsAsciiLetterOrDigit(c) || c == '.'));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">键的长度上限</param>
    /// <param name="Separator">段分隔符</param>
    /// <param name="ReservedSegment">保留段</param>
    /// <param name="Valid">合法的键</param>
    /// <param name="Invalid">不合法、也不在保留前缀下的键</param>
    /// <param name="Reserved">保留前缀下的键</param>
    private sealed record BlobKeyVectors(int MaxLength, string Separator, string ReservedSegment, BlobKeyCase[] Valid, BlobKeyCase[] Invalid, BlobKeyCase[] Reserved);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Key">键的文本</param>
    private sealed record BlobKeyCase(string Name, string Key);
}
