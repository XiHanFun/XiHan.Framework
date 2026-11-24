// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Multipart;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>multipart-parts.json</c>：分片编号的范围与完成清单的次序
/// </summary>
/// <remarks>
/// 规范 RFC-0012「范围读取、分片上传与预签名」：编号在 1 到 10000 之间，除最后一片外每片至少 5 MiB，完成清单按编号严格升序，空清单、乱序与重复都以 <c>OBJECTSTORAGE_VALIDATION_002</c> 失败（ADR-0049、ADR-0277）。清单的另外两项核对（ETag 与已上传的一致、非末片不过小）要有已上传的分片，由行为一致性套件 <c>BlobStoreConformance</c> 核对
/// </remarks>
public sealed class MultipartPartVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly MultipartPartVectors _vectors = ConformanceVectors.Load<MultipartPartVectors>("multipart-parts.json");

    [Fact]
    public void TheLimitsAndTheCode_AreTheOnesInTheVectors()
    {
        Assert.Equal(BlobMultipart.MaxPartNumber, _vectors.MaxPartNumber);
        Assert.Equal(BlobMultipart.MinPartBytes, _vectors.MinPartBytes);
        Assert.Equal(ObjectStorageErrors.PartsInvalid, _vectors.Code);
    }

    [Fact]
    public void ValidatePartNumber_AcceptsTheValidNumbers()
    {
        Assert.NotEmpty(_vectors.PartNumbers.Valid);
        foreach (var number in _vectors.PartNumbers.Valid)
        {
            Assert.Equal(number, BlobMultipart.ValidatePartNumber(number));
            Assert.Equal(number, Part(number).PartNumber);
        }
    }

    [Fact]
    public void ValidatePartNumber_RejectsTheInvalidNumbers()
    {
        Assert.NotEmpty(_vectors.PartNumbers.Invalid);
        foreach (var number in _vectors.PartNumbers.Invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BlobMultipart.ValidatePartNumber(number));
            Assert.Throws<ArgumentOutOfRangeException>(() => Part(number));
        }
    }

    [Fact]
    public void ValidateParts_JudgesTheOrderOfEveryManifest()
    {
        Assert.NotEmpty(_vectors.Manifests);
        var failures = new List<string>();
        foreach (var vector in _vectors.Manifests)
        {
            var result = BlobMultipart.ValidateParts([.. vector.Parts.Select(Part)]);
            if (result.IsSuccess != vector.Valid)
            {
                failures.Add($"{vector.Name}: the manifest was {(result.IsSuccess ? "accepted" : "rejected")}, the vector has valid = {vector.Valid}");
            }
            else if (!vector.Valid && result.Error?.Code != _vectors.Code)
            {
                failures.Add($"{vector.Name}: the manifest was rejected with '{result.Error?.Code}', the vector has '{_vectors.Code}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverBothEndsOfTheNumbers_AndEveryKindOfBadManifest()
    {
        // 防止向量换了写法后边界一条都没核对还照样通过：两头正好到限的要收，各差一个的要拒
        Assert.Contains(1, _vectors.PartNumbers.Valid);
        Assert.Contains(_vectors.MaxPartNumber, _vectors.PartNumbers.Valid);
        Assert.Contains(0, _vectors.PartNumbers.Invalid);
        Assert.Contains(_vectors.MaxPartNumber + 1, _vectors.PartNumbers.Invalid);

        // 清单：空、乱序、重复各至少一条不合法的，间隔着编号的与到上限的各至少一条合法的
        Assert.Contains(_vectors.Manifests, static vector => !vector.Valid && vector.Parts.Length == 0);
        Assert.Contains(_vectors.Manifests, static vector => !vector.Valid && vector.Parts.Zip(vector.Parts.Skip(1)).Any(static pair => pair.Second < pair.First));
        Assert.Contains(_vectors.Manifests, static vector => !vector.Valid && vector.Parts.Zip(vector.Parts.Skip(1)).Any(static pair => pair.Second == pair.First));
        Assert.Contains(_vectors.Manifests, static vector => vector.Valid && vector.Parts.Zip(vector.Parts.Skip(1)).Any(static pair => pair.Second > pair.First + 1));
        Assert.Contains(_vectors.Manifests, vector => vector.Valid && vector.Parts.LastOrDefault() == _vectors.MaxPartNumber);
    }

    /// <summary>
    /// 给定编号的分片
    /// </summary>
    /// <remarks>
    /// 清单的次序只看编号，ETag 取一个固定的值
    /// </remarks>
    private static BlobPart Part(int number) => new(number, "etag");

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxPartNumber">分片编号的上限</param>
    /// <param name="MinPartBytes">除最后一片外每片的最小字节数</param>
    /// <param name="Code">完成清单的次序不合法时的错误码</param>
    /// <param name="PartNumbers">分片编号的用例</param>
    /// <param name="Manifests">完成清单的用例</param>
    private sealed record MultipartPartVectors(int MaxPartNumber, long MinPartBytes, string Code, PartNumberCases PartNumbers, ManifestCase[] Manifests);

    /// <summary>
    /// 分片编号的用例
    /// </summary>
    /// <param name="Valid">范围内的编号</param>
    /// <param name="Invalid">范围外的编号</param>
    private sealed record PartNumberCases(int[] Valid, int[] Invalid);

    /// <summary>
    /// 一份完成清单
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Parts">清单里各片的编号，按清单的次序</param>
    /// <param name="Valid">这份清单的编号次序是否合法</param>
    private sealed record ManifestCase(string Name, int[] Parts, bool Valid);
}
