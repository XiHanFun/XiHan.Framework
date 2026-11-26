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
/// 规范 RFC-0012「标识」：名是 1 到 64 个小写字母、数字与连字符，字母或数字开头；值不含控制字符与孤立的代理项；名与值合计不超过 2048 个 UTF-8 字节（ADR-0214、ADR-0277、ADR-0322）
/// </remarks>
public sealed class BlobMetadataVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BlobMetadataVectors _vectors = SpecVectors.Read<BlobMetadataVectors>("objectstorage", "blob-metadata.json");

    /// <summary>
    /// 不换掉孤立代理项、遇到即抛出的 UTF-8 编码
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
            var values = vector.Values;
            var options = new BlobPutOptions { Metadata = values };

            Assert.True(values.Count == options.Metadata.Count && values.All(pair => options.Metadata[pair.Key] == pair.Value), vector.Name);
        }
    }

    [Fact]
    public void Metadata_RejectsTheInvalidSets()
    {
        Assert.NotEmpty(_vectors.Sets.Invalid);
        foreach (var vector in _vectors.Sets.Invalid)
        {
            var error = Record.Exception(() => new BlobPutOptions { Metadata = vector.Values });

            Assert.True(error is ArgumentException, vector.Name);
        }
    }

    [Fact]
    public void TheVectors_StateTheBytesTheyClaim_AndCoverBothSidesOfTheLimit()
    {
        // 向量自己标了字节数：先核对标注属实，下面按标注挑用例才可信。值里有孤立代理项的一组编不成 UTF-8，不标字节数（ADR-0322）
        Assert.All(_vectors.Sets.Valid.Concat(_vectors.Sets.Invalid), static vector => Assert.Equal(vector.Values.Values.All(Encodable) ? Utf8BytesOf(vector.Values) : (int?)null, vector.Utf8Bytes));

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

    [Fact]
    public void TheVectors_WriteValuesWithLoneSurrogatesAsCodeUnits_AndKeepValidPairs()
    {
        // 以码元写出的不合法值都真有孤立的代理项：没有的应当写成原文，读的人才看得出它为什么不合法（ADR-0322）
        var lone = _vectors.Sets.Invalid.Where(static vector => vector.Utf16 is not null).ToList();
        Assert.NotEmpty(lone);
        Assert.All(lone, static vector => Assert.Contains(vector.Utf16!, static pair => !Encodable(SpecVectors.FromCodeUnits(pair.Value))));

        // 成对的代理项照常合法；以码元写出的合法一组与转义写出的是同一组，组字符串的写法错了这里就对不上
        Assert.Contains(_vectors.Sets.Valid, static vector => vector.Metadata.Values.Any(static value => value.Any(char.IsSurrogate)));
        var byUnits = Assert.Single(_vectors.Sets.Valid, static vector => vector.Utf16 is not null);
        Assert.Contains(_vectors.Sets.Valid, vector => vector.Utf16 is null && vector.Metadata.Count == byUnits.Values.Count && vector.Metadata.All(pair => byUnits.Values.GetValueOrDefault(pair.Key) == pair.Value));
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
    /// <param name="Metadata">写得进 JSON 字符串的名到值</param>
    /// <param name="Utf16">值写不进 JSON 字符串的（含孤立的代理项）名到以四位十六进制写出的 UTF-16 码元；没有为 <see langword="null"/></param>
    /// <param name="Utf8Bytes">名与值的 UTF-8 字节数之和；有孤立代理项的一组编不成 UTF-8，为 <see langword="null"/></param>
    private sealed record MetadataSet(string Name, Dictionary<string, string> Metadata, Dictionary<string, string[]>? Utf16 = null, int? Utf8Bytes = null)
    {
        /// <summary>
        /// 整组元数据：<see cref="Metadata"/> 的各项与 <see cref="Utf16"/> 组出的各项合在一起
        /// </summary>
        /// <remarks>
        /// 同一个名在两处都写了是向量写错了，在这里失败（ADR-0322）
        /// </remarks>
        public Dictionary<string, string> Values
        {
            get
            {
                var values = new Dictionary<string, string>(Metadata, StringComparer.Ordinal);
                foreach (var (name, units) in Utf16 ?? [])
                {
                    Assert.True(values.TryAdd(name, SpecVectors.FromCodeUnits(units)), $"The case '{Name}' gives the metadata '{name}' both as text and as utf16 code units.");
                }

                return values;
            }
        }
    }
}
