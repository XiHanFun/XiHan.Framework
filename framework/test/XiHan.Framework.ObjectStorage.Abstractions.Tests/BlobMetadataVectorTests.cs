// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>blob-metadata.json</c>：自定义元数据的名、值与合计上限
/// </summary>
/// <remarks>
/// 规范 RFC-0012「标识」：名是 1 到 64 个小写字母、数字与连字符，字母或数字开头；值不含控制字符；名与值合计不超过 2048 个 UTF-8 字节（ADR-0214、ADR-0277）
/// </remarks>
public sealed class BlobMetadataVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BlobMetadataVectors _vectors = SpecVectors.Read<BlobMetadataVectors>("objectstorage", "blob-metadata.json");

    [Fact]
    public void MaxBytes_IsTheOneInTheVectors()
        => Assert.Equal(BlobPutOptions.MaxMetadataBytes, _vectors.MaxBytes);

    [Fact]
    public void IsValidMetadataName_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Names.Valid);
        foreach (var name in _vectors.Names.Valid)
        {
            Assert.True(BlobPutOptions.IsValidMetadataName(name), name);
            Assert.Single(new BlobPutOptions { Metadata = new Dictionary<string, string> { [name] = "v" } }.Metadata);
        }
    }

    [Fact]
    public void IsValidMetadataName_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Names.Invalid);
        foreach (var name in _vectors.Names.Invalid)
        {
            Assert.False(BlobPutOptions.IsValidMetadataName(name), name);
            Assert.Throws<ArgumentException>(() => new BlobPutOptions { Metadata = new Dictionary<string, string> { [name] = "v" } });
        }
    }

    [Fact]
    public void TheNameLengthLimit_IsTheOneInTheVectors()
    {
        // 元数据名没有长度常量，上限写在正则里：用只差长度的名字核对两头
        Assert.True(BlobPutOptions.IsValidMetadataName(new string('a', _vectors.Names.MaxLength)));
        Assert.False(BlobPutOptions.IsValidMetadataName(new string('a', _vectors.Names.MaxLength + 1)));
    }

    [Fact]
    public void Metadata_AcceptsTheValidSets()
    {
        Assert.NotEmpty(_vectors.Sets.Valid);
        foreach (var vector in _vectors.Sets.Valid)
        {
            var options = new BlobPutOptions { Metadata = vector.Metadata };

            Assert.True(vector.Metadata.Count == options.Metadata.Count && vector.Metadata.All(pair => options.Metadata[pair.Key] == pair.Value), vector.Name);
        }
    }

    [Fact]
    public void Metadata_RejectsTheInvalidSets()
    {
        Assert.NotEmpty(_vectors.Sets.Invalid);
        foreach (var vector in _vectors.Sets.Invalid)
        {
            var error = Record.Exception(() => new BlobPutOptions { Metadata = vector.Metadata });

            Assert.True(error is ArgumentException, vector.Name);
        }
    }

    [Fact]
    public void TheVectors_StateTheBytesTheyClaim_AndCoverBothSidesOfTheLimit()
    {
        // 向量自己标了字节数：先核对标注属实，下面按标注挑用例才可信
        Assert.All(_vectors.Sets.Valid.Concat(_vectors.Sets.Invalid), static vector => Assert.Equal(vector.Utf8Bytes, Utf8BytesOf(vector.Metadata)));

        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，多一个字节的要拒
        Assert.Contains(_vectors.Sets.Valid, vector => vector.Utf8Bytes == _vectors.MaxBytes && vector.Metadata.Count == 1);
        Assert.Contains(_vectors.Sets.Valid, vector => vector.Utf8Bytes == _vectors.MaxBytes && vector.Metadata.Count > 1);
        Assert.Contains(_vectors.Sets.Invalid, vector => vector.Utf8Bytes == _vectors.MaxBytes + 1 && vector.Metadata.Count == 1);
        Assert.Contains(_vectors.Sets.Invalid, vector => vector.Utf8Bytes == _vectors.MaxBytes + 1 && vector.Metadata.Count > 1);

        // 合计按字节计：按 UTF-16 码元数不超而按字节数超的要拒，按码元计的实现会把它放过去
        Assert.Contains(_vectors.Sets.Invalid, vector => vector.Utf8Bytes > _vectors.MaxBytes && Utf16UnitsOf(vector.Metadata) <= _vectors.MaxBytes);

        // 名的上限同理
        Assert.Contains(_vectors.Names.Valid, name => name.Length == _vectors.Names.MaxLength);
        Assert.Contains(_vectors.Names.Invalid, name => name.Length == _vectors.Names.MaxLength + 1 && name.All(char.IsAsciiLetterOrDigit));
    }

    /// <summary>
    /// 一组元数据的名与值的 UTF-8 字节数之和
    /// </summary>
    private static int Utf8BytesOf(Dictionary<string, string> metadata)
        => metadata.Sum(static pair => Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value));

    /// <summary>
    /// 一组元数据的名与值的 UTF-16 码元数之和
    /// </summary>
    private static int Utf16UnitsOf(Dictionary<string, string> metadata)
        => metadata.Sum(static pair => pair.Key.Length + pair.Value.Length);

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Names">元数据名的用例</param>
    /// <param name="MaxBytes">名与值合计的 UTF-8 字节数上限</param>
    /// <param name="Sets">整组元数据的用例</param>
    private sealed record BlobMetadataVectors(MetadataNameCases Names, int MaxBytes, MetadataSetCases Sets);

    /// <summary>
    /// 元数据名的用例
    /// </summary>
    /// <param name="MaxLength">名的长度上限</param>
    /// <param name="Valid">合法的名</param>
    /// <param name="Invalid">不合法的名</param>
    private sealed record MetadataNameCases(int MaxLength, string[] Valid, string[] Invalid);

    /// <summary>
    /// 整组元数据的用例
    /// </summary>
    /// <param name="Valid">合法的</param>
    /// <param name="Invalid">不合法的</param>
    private sealed record MetadataSetCases(MetadataSet[] Valid, MetadataSet[] Invalid);

    /// <summary>
    /// 一个对象的整组元数据
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Metadata">名到值</param>
    /// <param name="Utf8Bytes">名与值的 UTF-8 字节数之和</param>
    private sealed record MetadataSet(string Name, Dictionary<string, string> Metadata, int Utf8Bytes);
}
