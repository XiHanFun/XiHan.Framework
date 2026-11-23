// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 完成清单本身的校验由抽象包给出，各 Provider 共用同一个函数
/// </summary>
/// <remarks>
/// 原先文件系统与 S3 各写一遍：S3 那份不查空项，清单里有 <see langword="null"/> 时抛的是 <see cref="NullReferenceException"/>
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

    /// <summary>
    /// 只有编号有意义的分片
    /// </summary>
    private static BlobPart Part(int number) => new(number, "0123456789abcdef0123456789abcdef", 1);
}
