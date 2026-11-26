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
/// 规范 RFC-0006「存储契约」与「键管理」：长度按 UTF-8 编码后的字节数计，不超过 512 字节（ADR-0274、ADR-0276），不含孤立的代理项（ADR-0316，向量里以 UTF-16 码元写出）。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class CacheKeyVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly CacheKeyVectors _vectors = SpecVectors.Read<CacheKeyVectors>("caching", "cache-keys.json");

    /// <summary>
    /// 严格的 UTF-8 编码：编不成即抛出，用来认出含孤立代理项的用例
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    [Fact]
    public void MaxBytes_IsTheOneInTheVectors()
        => Assert.Equal(CacheKey.MaxBytes, _vectors.MaxBytes);

    [Fact]
    public void IsValid_AcceptsTheValidKeys()
    {
        Assert.NotEmpty(_vectors.Keys.Valid);
        foreach (var key in _vectors.Keys.Valid)
        {
            Assert.True(CacheKey.IsValid(key.Input), key.Name);
            Assert.Equal(key.Input, CacheKey.Validate(key.Input));
            Assert.Equal(key.Input, new CacheKey(key.Input).Value);
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidKeys()
    {
        Assert.NotEmpty(_vectors.Keys.Invalid);
        foreach (var key in _vectors.Keys.Invalid)
        {
            Assert.False(CacheKey.IsValid(key.Input), key.Name);
            Assert.Throws<ArgumentException>(() => CacheKey.Validate(key.Input));
            Assert.Throws<ArgumentException>(() => new CacheKey(key.Input));
        }
    }

    [Fact]
    public void IsValidPrefix_AcceptsTheValidPrefixes()
    {
        Assert.NotEmpty(_vectors.Prefixes.Valid);
        Assert.All(_vectors.Prefixes.Valid, static prefix => Assert.True(CacheKey.IsValidPrefix(prefix.Input), prefix.Name));
    }

    [Fact]
    public void IsValidPrefix_RejectsTheInvalidPrefixes()
    {
        Assert.NotEmpty(_vectors.Prefixes.Invalid);
        Assert.All(_vectors.Prefixes.Invalid, static prefix => Assert.False(CacheKey.IsValidPrefix(prefix.Input), prefix.Name));
    }

    [Fact]
    public void TheVectors_StateTheLengthsTheyClaim_AndCoverBothSidesOfTheLimit()
    {
        // 向量自己标了字节数与码元数：先核对标注属实，下面按标注挑用例才可信；
        // 含孤立代理项的编不成 UTF-8，不标字节数，只核对码元数
        var all = _vectors.Keys.Valid.Concat(_vectors.Keys.Invalid).Concat(_vectors.Prefixes.Valid).Concat(_vectors.Prefixes.Invalid).ToList();
        Assert.All(all, static entry =>
        {
            Assert.Equal(Encodable(entry.Input) ? Encoding.UTF8.GetByteCount(entry.Input) : null, entry.Utf8Bytes);
            Assert.Equal(entry.Utf16Units, entry.Input.Length);
        });

        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收；按码元数不超、按字节数超的要拒（这正是改单位的那一类）
        Assert.Contains(_vectors.Keys.Valid, key => key.Utf8Bytes == _vectors.MaxBytes);
        Assert.Contains(_vectors.Keys.Invalid, key => key.Utf8Bytes == _vectors.MaxBytes + 1);
        Assert.Contains(_vectors.Keys.Invalid, key => key.Utf8Bytes > _vectors.MaxBytes && key.Utf16Units <= _vectors.MaxBytes);
        Assert.Contains(_vectors.Prefixes.Invalid, prefix => prefix.Utf8Bytes > _vectors.MaxBytes && prefix.Utf16Units <= _vectors.MaxBytes);
    }

    [Fact]
    public void TheVectors_WriteLoneSurrogatesAsCodeUnits_AndKeepValidPairs()
    {
        // 以码元写出的不合法用例都真有孤立的代理项：没有的应当写成原文，读的人才看得出它为什么不合法（ADR-0316）
        var lone = _vectors.Keys.Invalid.Concat(_vectors.Prefixes.Invalid).Where(static entry => entry.Utf16 is not null).ToList();
        Assert.NotEmpty(lone);
        Assert.All(lone, static entry => Assert.False(Encodable(entry.Input), entry.Name));
        Assert.Contains(_vectors.Prefixes.Invalid, static entry => entry.Utf16 is not null);

        // 成对的代理项是平面以外的一个字符，照常合法
        Assert.Contains(_vectors.Keys.Valid, static entry => entry.Input.Any(char.IsSurrogate));
        Assert.Contains(_vectors.Prefixes.Valid, static entry => entry.Input.Any(char.IsSurrogate));
    }

    /// <summary>
    /// 文本能否严格编码成 UTF-8：含孤立的代理项即不能
    /// </summary>
    private static bool Encodable(string text)
    {
        try
        {
            _strictUtf8.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
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
    /// <param name="Utf16Units">UTF-16 码元数</param>
    /// <param name="Text">键或前缀的文本；JSON 字符串写不出的（孤立的代理项）为 <see langword="null"/></param>
    /// <param name="Utf16">以四位十六进制写出的 UTF-16 码元；写了原文的为 <see langword="null"/></param>
    /// <param name="Utf8Bytes">UTF-8 编码后的字节数；编不成 UTF-8 的为 <see langword="null"/></param>
    private sealed record CacheKeyCase(string Name, int Utf16Units, string? Text = null, string[]? Utf16 = null, int? Utf8Bytes = null)
    {
        /// <summary>
        /// 键或前缀的文本：原文，或由码元组成
        /// </summary>
        public string Input => SpecVectors.TextOf(Name, Text, Utf16);
    }
}
