// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>download-file-names.json</c>：预签名下载地址上保存的文件名
/// </summary>
/// <remarks>
/// 规范 RFC-0012「范围读取、分片上传与预签名」：文件名不为空、不全是空白、不含控制字符，长度按 UTF-8 编码后的字节数计，不超过 255 字节；不合法在构造预签名请求时抛出（ADR-0219、ADR-0282）
/// </remarks>
public sealed class DownloadFileNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly DownloadFileNameVectors _vectors = ConformanceVectors.Load<DownloadFileNameVectors>("download-file-names.json");

    [Fact]
    public void MaxBytes_IsTheOneInTheVectors()
        => Assert.Equal(BlobUrlRequest.MaxDownloadFileNameBytes, _vectors.MaxBytes);

    [Fact]
    public void ForDownload_AcceptsTheValidFileNames()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var vector in _vectors.Valid)
        {
            var request = BlobUrlRequest.ForDownload(null, vector.Text);

            // 文件名原样保留，不裁剪首尾的空格
            Assert.True(request.DownloadFileName == vector.Text, vector.Name);
            // 合法的文件名都写得成响应头：不抛出，头里没有换行
            Assert.DoesNotContain('\n', BlobContentDisposition.Attachment(vector.Text));
        }
    }

    [Fact]
    public void ForDownload_RejectsTheInvalidFileNames()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var vector in _vectors.Invalid)
        {
            var error = Record.Exception(() => BlobUrlRequest.ForDownload(null, vector.Text));

            Assert.True(error is ArgumentException, vector.Name);
        }
    }

    [Fact]
    public void TheVectors_StateTheLengthsTheyClaim_AndCoverBothSidesOfTheLimit()
    {
        // 向量自己标了字节数与码元数：先核对标注属实，下面按标注挑用例才可信
        Assert.All(_vectors.Valid.Concat(_vectors.Invalid), static vector =>
        {
            Assert.True(vector.Utf8Bytes == Encoding.UTF8.GetByteCount(vector.Text), vector.Name);
            Assert.True(vector.Utf16Units == vector.Text.Length, vector.Name);
        });

        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，多 1 个字节的要拒；
        // 按码元数不超、按字节数超的要拒（这正是改单位的那一类），其中有恰好多 1 个字节的
        var max = _vectors.MaxBytes;
        Assert.Contains(_vectors.Valid, vector => vector.Utf8Bytes == max && vector.Utf16Units == max);
        Assert.Contains(_vectors.Valid, vector => vector.Utf8Bytes == max && vector.Utf16Units < max);
        Assert.Contains(_vectors.Invalid, vector => vector.Utf8Bytes == max + 1 && vector.Utf16Units == max + 1);
        Assert.Contains(_vectors.Invalid, vector => vector.Utf8Bytes == max + 1 && vector.Utf16Units <= max);
        Assert.Contains(_vectors.Invalid, vector => vector.Utf8Bytes > max && vector.Utf16Units == max);

        // 长度之外的两条：全是空白的不收，带控制字符的不收
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Length > 0 && vector.Text.All(char.IsWhiteSpace) && !vector.Text.Any(char.IsControl));
        Assert.Contains(_vectors.Invalid, static vector => vector.Text.Any(char.IsControl) && !vector.Text.All(char.IsWhiteSpace));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxBytes">文件名按 UTF-8 编码后的字节数上限</param>
    /// <param name="Valid">合法的文件名</param>
    /// <param name="Invalid">不合法的文件名</param>
    private sealed record DownloadFileNameVectors(int MaxBytes, DownloadFileNameCase[] Valid, DownloadFileNameCase[] Invalid);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Text">文件名的文本</param>
    /// <param name="Utf8Bytes">UTF-8 编码后的字节数</param>
    /// <param name="Utf16Units">UTF-16 码元数</param>
    private sealed record DownloadFileNameCase(string Name, string Text, int Utf8Bytes, int Utf16Units);
}
