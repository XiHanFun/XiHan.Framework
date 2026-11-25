// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 回放 <c>cache-keys.json</c>：缓存键与键前缀的文法和长度上限
/// </summary>
/// <remarks>
/// 规范 RFC-0006「存储契约」与「键管理」：长度按 UTF-8 编码后的字节数计，不超过 512 字节（ADR-0274、ADR-0276）。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class CacheKeyVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly CacheKeyVectors _vectors = SpecVectors.Read<CacheKeyVectors>("caching", "cache-keys.json");

    [Fact]
    public void MaxBytes_IsTheOneInTheVectors()
        => Assert.Equal(CacheKey.MaxBytes, _vectors.MaxBytes);

    [Fact]
    public void IsValid_AcceptsTheValidKeys()
    {
        Assert.NotEmpty(_vectors.Keys.Valid);
        foreach (var key in _vectors.Keys.Valid)
        {
            Assert.True(CacheKey.IsValid(key.Text), key.Name);
            Assert.Equal(key.Text, CacheKey.Validate(key.Text));
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidKeys()
    {
        Assert.NotEmpty(_vectors.Keys.Invalid);
        foreach (var key in _vectors.Keys.Invalid)
        {
            Assert.False(CacheKey.IsValid(key.Text), key.Name);
            Assert.Throws<ArgumentException>(() => CacheKey.Validate(key.Text));
        }
    }

    [Fact]
    public void IsValidPrefix_AcceptsTheValidPrefixes()
    {
        Assert.NotEmpty(_vectors.Prefixes.Valid);
        Assert.All(_vectors.Prefixes.Valid, static prefix => Assert.True(CacheKey.IsValidPrefix(prefix.Text), prefix.Name));
    }

    [Fact]
    public void IsValidPrefix_RejectsTheInvalidPrefixes()
    {
        Assert.NotEmpty(_vectors.Prefixes.Invalid);
        Assert.All(_vectors.Prefixes.Invalid, static prefix => Assert.False(CacheKey.IsValidPrefix(prefix.Text), prefix.Name));
    }

    [Fact]
    public void TheVectors_StateTheLengthsTheyClaim_AndCoverBothSidesOfTheLimit()
    {
        // 向量自己标了字节数与码元数：先核对标注属实，下面按标注挑用例才可信
        var all = _vectors.Keys.Valid.Concat(_vectors.Keys.Invalid).Concat(_vectors.Prefixes.Valid).Concat(_vectors.Prefixes.Invalid).ToList();
        Assert.All(all, static entry =>
        {
            Assert.Equal(entry.Utf8Bytes, Encoding.UTF8.GetByteCount(entry.Text));
            Assert.Equal(entry.Utf16Units, entry.Text.Length);
        });

        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收；按码元数不超、按字节数超的要拒（这正是改单位的那一类）
        Assert.Contains(_vectors.Keys.Valid, key => key.Utf8Bytes == _vectors.MaxBytes);
        Assert.Contains(_vectors.Keys.Invalid, key => key.Utf8Bytes == _vectors.MaxBytes + 1);
        Assert.Contains(_vectors.Keys.Invalid, key => key.Utf8Bytes > _vectors.MaxBytes && key.Utf16Units <= _vectors.MaxBytes);
        Assert.Contains(_vectors.Prefixes.Invalid, prefix => prefix.Utf8Bytes > _vectors.MaxBytes && prefix.Utf16Units <= _vectors.MaxBytes);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxBytes">键与前缀的字节数上限</param>
    /// <param name="Keys">键的用例</param>
    /// <param name="Prefixes">键前缀的用例</param>
    private sealed record CacheKeyVectors(int MaxBytes, CacheKeyCases Keys, CacheKeyCases Prefixes);

    /// <summary>
    /// 一组合法与不合法的用例
    /// </summary>
    /// <param name="Valid">合法的</param>
    /// <param name="Invalid">不合法的</param>
    private sealed record CacheKeyCases(CacheKeyCase[] Valid, CacheKeyCase[] Invalid);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Text">键或前缀的文本</param>
    /// <param name="Utf8Bytes">UTF-8 编码后的字节数</param>
    /// <param name="Utf16Units">UTF-16 码元数</param>
    private sealed record CacheKeyCase(string Name, string Text, int Utf8Bytes, int Utf16Units);
}
