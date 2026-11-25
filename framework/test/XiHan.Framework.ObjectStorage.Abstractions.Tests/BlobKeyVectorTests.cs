// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>blob-keys.json</c>：对象键与查询前缀的文法、长度上限与保留前缀
/// </summary>
/// <remarks>
/// 规范 RFC-0012「标识」：<c>/</c> 分段的路径，每段非空、不是 <c>.</c> 或 <c>..</c>，不含空白、控制字符与反斜线，长度按 UTF-8 编码后的字节数计，不超过 1024 字节；首段是保留段的键留给框架自己的对象，不在键文法之内；查询前缀的字符约束与字节上限同键，可以为空、对各段不作要求（ADR-0090、ADR-0277、ADR-0282）
/// </remarks>
public sealed class BlobKeyVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BlobKeyVectors _vectors = SpecVectors.Read<BlobKeyVectors>("objectstorage", "blob-keys.json");

    [Fact]
    public void TheConstants_AreTheOnesInTheVectors()
    {
        Assert.Equal(BlobKey.MaxBytes, _vectors.MaxBytes);
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
    public void IsValidPrefix_AcceptsTheValidPrefixes()
    {
        Assert.NotEmpty(_vectors.Prefixes.Valid);
        foreach (var vector in _vectors.Prefixes.Valid)
        {
            Assert.True(BlobKey.IsValidPrefix(vector.Prefix), vector.Name);
            Assert.Equal(vector.Prefix, new BlobQuery { Prefix = vector.Prefix }.Prefix);
        }
    }

    [Fact]
    public void IsValidPrefix_RejectsTheInvalidPrefixes()
    {
        Assert.NotEmpty(_vectors.Prefixes.Invalid);
        foreach (var vector in _vectors.Prefixes.Invalid)
        {
            Assert.False(BlobKey.IsValidPrefix(vector.Prefix), vector.Name);
            Assert.Throws<ArgumentException>(() => new BlobQuery { Prefix = vector.Prefix });
        }
    }

    [Fact]
    public void TheVectors_StateTheLengthsTheyClaim_AndCoverBothSidesOfTheLimit()
    {
        // 向量自己标了字节数与码元数：先核对标注属实，下面按标注挑用例才可信
        var keys = _vectors.Valid.Concat(_vectors.Invalid).Concat(_vectors.Reserved).Select(static vector => (vector.Name, Text: vector.Key, vector.Utf8Bytes, vector.Utf16Units));
        var prefixes = _vectors.Prefixes.Valid.Concat(_vectors.Prefixes.Invalid).Select(static vector => (vector.Name, Text: vector.Prefix, vector.Utf8Bytes, vector.Utf16Units));
        Assert.All(keys.Concat(prefixes), static entry =>
        {
            Assert.True(entry.Utf8Bytes == Encoding.UTF8.GetByteCount(entry.Text), entry.Name);
            Assert.True(entry.Utf16Units == entry.Text.Length, entry.Name);
        });

        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，多 1 个字节的要拒；
        // 按码元数不超、按字节数超的要拒（这正是改单位的那一类），其中有恰好多 1 个字节的
        var max = _vectors.MaxBytes;
        Assert.Contains(_vectors.Valid, vector => vector.Utf8Bytes == max && vector.Utf16Units == max);
        Assert.Contains(_vectors.Valid, vector => vector.Utf8Bytes == max && vector.Utf16Units < max);
        Assert.Contains(_vectors.Invalid, vector => vector.Utf8Bytes == max + 1 && vector.Utf16Units == max + 1);
        Assert.Contains(_vectors.Invalid, vector => vector.Utf8Bytes == max + 1 && vector.Utf16Units <= max);
        Assert.Contains(_vectors.Invalid, vector => vector.Utf8Bytes > max && vector.Utf16Units == max);
        Assert.Contains(_vectors.Prefixes.Valid, vector => vector.Utf8Bytes == max);
        Assert.Contains(_vectors.Prefixes.Invalid, vector => vector.Utf8Bytes == max + 1 && vector.Utf16Units == max + 1);
        Assert.Contains(_vectors.Prefixes.Invalid, vector => vector.Utf8Bytes > max && vector.Utf16Units <= max);
    }

    [Fact]
    public void TheVectors_CoverTheEdgesOfTheReservedPrefix_AndWhatOnlyAPrefixMayBe()
    {
        // 保留前缀只看首段、区分大小写：保留段本身与它下面的键是保留的，只是像它的键照常合法
        var under = _vectors.ReservedSegment + _vectors.Separator;
        Assert.Contains(_vectors.Reserved, vector => vector.Key == _vectors.ReservedSegment);
        Assert.Contains(_vectors.Reserved, vector => vector.Key.StartsWith(under, StringComparison.Ordinal));
        Assert.Contains(_vectors.Valid, vector => vector.Key.StartsWith(_vectors.ReservedSegment, StringComparison.Ordinal));
        Assert.Contains(_vectors.Valid, vector => vector.Key.StartsWith(under, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_vectors.Valid, vector => vector.Key.Contains(_vectors.Separator + under, StringComparison.Ordinal));

        // 前缀比键宽的三处：可以为空、可以以分隔符结尾、不受保留前缀限制
        Assert.Contains(_vectors.Prefixes.Valid, static vector => vector.Prefix.Length == 0);
        Assert.Contains(_vectors.Prefixes.Valid, vector => vector.Prefix.EndsWith(_vectors.Separator, StringComparison.Ordinal) && !BlobKey.IsValid(vector.Prefix));
        Assert.Contains(_vectors.Prefixes.Valid, static vector => BlobKey.IsReserved(vector.Prefix.TrimEnd(BlobKey.Separator)));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxBytes">键与查询前缀按 UTF-8 编码后的字节数上限</param>
    /// <param name="Separator">段分隔符</param>
    /// <param name="ReservedSegment">保留段</param>
    /// <param name="Valid">合法的键</param>
    /// <param name="Invalid">不合法、也不在保留前缀下的键</param>
    /// <param name="Reserved">保留前缀下的键</param>
    /// <param name="Prefixes">查询前缀的用例</param>
    private sealed record BlobKeyVectors(int MaxBytes, string Separator, string ReservedSegment, BlobKeyCase[] Valid, BlobKeyCase[] Invalid, BlobKeyCase[] Reserved, BlobKeyPrefixCases Prefixes);

    /// <summary>
    /// 一个键的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Key">键的文本</param>
    /// <param name="Utf8Bytes">UTF-8 编码后的字节数</param>
    /// <param name="Utf16Units">UTF-16 码元数</param>
    private sealed record BlobKeyCase(string Name, string Key, int Utf8Bytes, int Utf16Units);

    /// <summary>
    /// 一组合法与不合法的查询前缀
    /// </summary>
    /// <param name="Valid">合法的</param>
    /// <param name="Invalid">不合法的</param>
    private sealed record BlobKeyPrefixCases(BlobKeyPrefixCase[] Valid, BlobKeyPrefixCase[] Invalid);

    /// <summary>
    /// 一个查询前缀的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Prefix">前缀的文本</param>
    /// <param name="Utf8Bytes">UTF-8 编码后的字节数</param>
    /// <param name="Utf16Units">UTF-16 码元数</param>
    private sealed record BlobKeyPrefixCase(string Name, string Prefix, int Utf8Bytes, int Utf16Units);
}
