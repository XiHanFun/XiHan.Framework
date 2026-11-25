// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>blob-ranges.json</c>：范围读取实际读出的范围
/// </summary>
/// <remarks>
/// 规范 RFC-0012「范围读取、分片上传与预签名」：长度超出末尾即截断，起点不在对象内（含空对象）时读取以 <c>OBJECTSTORAGE_VALIDATION_001</c> 失败（ADR-0049、ADR-0277）。范围的算法在 <see cref="BlobRange.Resolve"/>，没有值就是起点不在对象内；各存储读取时报出这个错误码由行为一致性套件 <c>BlobStoreConformance</c> 核对
/// </remarks>
public sealed class BlobRangeVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BlobRangeVectors _vectors = SpecVectors.Read<BlobRangeVectors>("objectstorage", "blob-ranges.json");

    [Fact]
    public void Code_IsTheRangeNotSatisfiableError()
        => Assert.Equal(ObjectStorageErrors.RangeNotSatisfiable, _vectors.Code);

    [Fact]
    public void Resolve_GivesTheRangeOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var resolved = new BlobRange(vector.Offset, vector.Length).Resolve(vector.Total);
            var expected = vector.Range is { } range ? Maybe.Some(new BlobContentRange(range.Offset, range.Length, range.Total)) : Maybe.None<BlobContentRange>();
            if (resolved != expected)
            {
                failures.Add($"{vector.Name}: resolved '{resolved}', the vector has '{expected}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTruncationTheEndOfTheBlobAndLargeNumbers()
    {
        // 防止向量换了写法后边界一条都没核对还照样通过
        // 不给长度读到末尾、长度超出末尾截断
        Assert.Contains(_vectors.Cases, static vector => vector.Length is null && vector.Range is not null);
        Assert.Contains(_vectors.Cases, static vector => vector is { Length: { } length, Range: { } range } && range.Length < length);

        // 起点正好是总长、超出总长、空对象都没有范围；最后一个字节还读得到
        Assert.Contains(_vectors.Cases, static vector => vector.Range is null && vector.Offset == vector.Total && vector.Total > 0);
        Assert.Contains(_vectors.Cases, static vector => vector.Range is null && vector.Offset > vector.Total);
        Assert.Contains(_vectors.Cases, static vector => vector.Range is null && vector.Total == 0);
        Assert.Contains(_vectors.Cases, static vector => vector.Range is not null && vector.Offset == vector.Total - 1);

        // 超过 32 位的数字：按 32 位算或把起点与长度相加的实现会在这里出错
        Assert.Contains(_vectors.Cases, static vector => vector.Offset > uint.MaxValue);
        Assert.Contains(_vectors.Cases, static vector => vector.Length > uint.MaxValue && vector.Range is not null);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Code">起点不在对象内时读取失败的错误码</param>
    /// <param name="Cases">用例</param>
    private sealed record BlobRangeVectors(string Code, BlobRangeCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Offset">请求的起点</param>
    /// <param name="Length">请求的长度；<see langword="null"/> 表示读到末尾</param>
    /// <param name="Total">对象的总长</param>
    /// <param name="Range">实际读出的范围；<see langword="null"/> 表示起点不在对象内</param>
    private sealed record BlobRangeCase(string Name, long Offset, long? Length, long Total, ResolvedRange? Range);

    /// <summary>
    /// 实际读出的范围
    /// </summary>
    /// <param name="Offset">起点</param>
    /// <param name="Length">长度</param>
    /// <param name="Total">对象的总长</param>
    private sealed record ResolvedRange(long Offset, long Length, long Total);
}
