// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>blob-content-types.json</c>：内容类型的文法、长度上限与缺省值
/// </summary>
/// <remarks>
/// 规范 RFC-0012「标识」：媒体类型 <c>type/subtype</c>，两段都是 HTTP 的 token 字符，可跟 <c>;</c> 起头的参数，整体 1 到 255 个字符，按整串匹配（ADR-0214、ADR-0277）
/// </remarks>
public sealed class BlobContentTypeVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ContentTypeVectors _vectors = ConformanceVectors.Load<ContentTypeVectors>("blob-content-types.json");

    [Fact]
    public void TheLimitAndTheDefault_AreTheOnesInTheVectors()
    {
        Assert.Equal(BlobPutOptions.MaxContentTypeLength, _vectors.MaxLength);
        Assert.Equal(BlobPutOptions.DefaultContentType, _vectors.Default);

        // 写入时不给内容类型就是缺省值，它自己也合文法
        Assert.Equal(_vectors.Default, new BlobPutOptions().ContentType);
        Assert.Equal(_vectors.Default, BlobPutOptions.Default.ContentType);
        Assert.True(BlobPutOptions.IsValidContentType(_vectors.Default));
    }

    [Fact]
    public void IsValidContentType_AcceptsTheValidContentTypes()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var contentType in _vectors.Valid)
        {
            Assert.True(BlobPutOptions.IsValidContentType(contentType), contentType);
            Assert.Equal(contentType, new BlobPutOptions { ContentType = contentType }.ContentType);
        }
    }

    [Fact]
    public void IsValidContentType_RejectsTheInvalidContentTypes()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var contentType in _vectors.Invalid)
        {
            Assert.False(BlobPutOptions.IsValidContentType(contentType), contentType);
            Assert.Throws<ArgumentException>(() => new BlobPutOptions { ContentType = contentType });
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, contentType => contentType.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, contentType => contentType.Length == _vectors.MaxLength + 1 && IsWellFormedApartFromLength(contentType));
    }

    /// <summary>
    /// 除长度外是否合文法
    /// </summary>
    /// <remarks>
    /// 只认向量里用来卡长度的那种写法，不是完整的文法：恰好一个 <c>/</c>，两段非空，只含 ASCII 字母、数字与连字符
    /// </remarks>
    private static bool IsWellFormedApartFromLength(string contentType)
        => contentType.Split('/') is { Length: 2 } parts && parts.All(static part => part.Length > 0 && part.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-'));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">长度上限</param>
    /// <param name="Default">写入时不给内容类型的缺省值</param>
    /// <param name="Valid">合法的内容类型</param>
    /// <param name="Invalid">不合法的内容类型</param>
    private sealed record ContentTypeVectors(int MaxLength, string Default, string[] Valid, string[] Invalid);
}
