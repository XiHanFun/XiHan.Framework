// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 完成清单与批量放弃的集合本身的校验由抽象包给出，各 Provider 共用同一个函数
/// </summary>
/// <remarks>
/// 完成清单原先文件系统与 S3 各写一遍：S3 那份不查空项，清单里有 <see langword="null"/> 时抛的是 <see cref="NullReferenceException"/>。批量放弃的集合（ADR-0266）同样只在一处核对
/// </remarks>
public sealed class BlobMultipartTests
{
    [Fact]
    public void ValidateParts_AcceptsAStrictlyAscendingList()
    {
        Assert.True(BlobMultipart.ValidateParts([Part(1)]).IsSuccess);
        Assert.True(BlobMultipart.ValidateParts([Part(1), Part(3), Part(BlobMultipart.MaxPartNumber)]).IsSuccess);
    }

    [Fact]
    public void ValidateParts_RejectsAnEmptyList()
    {
        var result = BlobMultipart.ValidateParts([]);

        Assert.Equal(ObjectStorageErrors.PartsInvalid, result.Error?.Code);
        Assert.Equal(ObjectStorageMessages.PartsEmpty, result.Error?.MessageKey);
    }

    [Theory]
    [InlineData(new[] { 2, 1 }, 1)]
    [InlineData(new[] { 1, 1 }, 1)]
    [InlineData(new[] { 1, 5, 3, 9 }, 3)]
    public void ValidateParts_RejectsRepeatedOrDescendingNumbers_NamingTheFirstOffender(int[] numbers, int offender)
    {
        var result = BlobMultipart.ValidateParts([.. numbers.Select(Part)]);

        Assert.Equal(ObjectStorageErrors.PartsInvalid, result.Error?.Code);
        Assert.Equal(ObjectStorageMessages.PartsUnordered, result.Error?.MessageKey);
        Assert.Equal(offender, result.Error?.Details?["part"]);
    }

    [Fact]
    public void ValidateParts_ThrowsOnANullPart_WhereverItIs()
    {
        Assert.Throws<ArgumentException>(static () => BlobMultipart.ValidateParts([null!]));
        Assert.Throws<ArgumentException>(static () => BlobMultipart.ValidateParts([Part(1), null!]));
        Assert.Throws<ArgumentNullException>(static () => BlobMultipart.ValidateParts(null!));
    }

    [Fact]
    public void UploadIds_WithLoneSurrogates_AreRejected_AndSurrogatePairsAreKept()
    {
        // 上传标识进宿主代签地址的查询串与签名的规范串：孤立的代理项经宽松编码会签成另一个标识（RFC-0012「标识」，ADR-0316）
        foreach (var uploadId in new[] { "u\uD800", "u\uDC00v", "u\uDE00\uD83Dv", "\uDBFF" })
        {
            var error = Record.Exception(() => new BlobMultipartUpload(new ContainerName("uploads"), new BlobKey("a"), uploadId) { Overwrite = true });
            Assert.True(error is ArgumentException, $"The upload ID with {uploadId.Length} code units must be rejected.");
        }

        Assert.Equal("u\U0001F600", new BlobMultipartUpload(new ContainerName("uploads"), new BlobKey("a"), "u\U0001F600") { Overwrite = true }.UploadId);
    }

    [Fact]
    public void ValidateUploads_AcceptsAnyListWithoutNull_IncludingTheEmptyOneAndRepeats()
    {
        var upload = Upload("a");

        BlobMultipart.ValidateUploads([]);
        BlobMultipart.ValidateUploads([upload]);
        BlobMultipart.ValidateUploads([upload, Upload("b"), upload]);
    }

    [Fact]
    public void ValidateUploads_ThrowsOnANullUpload_WhereverItIs()
    {
        Assert.Throws<ArgumentException>(static () => BlobMultipart.ValidateUploads([null!]));
        Assert.Throws<ArgumentException>(static () => BlobMultipart.ValidateUploads([Upload("a"), null!]));
        Assert.Throws<ArgumentNullException>(static () => BlobMultipart.ValidateUploads(null!));
    }

    /// <summary>
    /// 只有键有意义的上传句柄
    /// </summary>
    private static BlobMultipartUpload Upload(string key) => new(new ContainerName("uploads"), new BlobKey(key), "0123456789abcdef0123456789abcdef") { Overwrite = true };

    /// <summary>
    /// 只有编号有意义的分片
    /// </summary>
    private static BlobPart Part(int number) => new(number, "0123456789abcdef0123456789abcdef", 1);
}
