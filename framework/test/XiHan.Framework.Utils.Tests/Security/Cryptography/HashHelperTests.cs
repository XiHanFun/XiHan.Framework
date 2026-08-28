// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// 哈希辅助类测试
/// </summary>
/// <remarks>
/// 哈希是纯函数，价值全在「固定输入必得固定输出」。这里锁死的向量取自 NIST/RFC 公开用例，
/// 一旦有人把内部编码从 UTF-8 换掉、把 <see cref="Convert.ToHexString"/> 换成 Base64 或小写、
/// 或者悄悄换了摘要算法，这些断言会立刻变红。
/// </remarks>
public class HashHelperTests : IDisposable
{
    private const string ChineseSample = "曦寒框架·中文哈希测试";

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备文件流哈希用的临时目录
    /// </summary>
    public HashHelperTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// MD5 固定向量
    /// </summary>
    [Theory]
    [InlineData("", "D41D8CD98F00B204E9800998ECF8427E")]
    [InlineData("abc", "900150983CD24FB0D6963F7D28E17F72")]
    public void Md5_ForKnownInput_MatchesPublishedVector(string input, string expected)
    {
        Assert.Equal(expected, HashHelper.Md5(input));
    }

    /// <summary>
    /// SHA1 固定向量
    /// </summary>
    [Theory]
    [InlineData("", "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709")]
    [InlineData("abc", "A9993E364706816ABA3E25717850C26C9CD0D89D")]
    public void Sha1_ForKnownInput_MatchesPublishedVector(string input, string expected)
    {
        Assert.Equal(expected, HashHelper.Sha1(input));
    }

    /// <summary>
    /// SHA256 固定向量
    /// </summary>
    [Theory]
    [InlineData("", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]
    [InlineData("abc", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    public void Sha256_ForKnownInput_MatchesPublishedVector(string input, string expected)
    {
        Assert.Equal(expected, HashHelper.Sha256(input));
    }

    /// <summary>
    /// SHA384 固定向量
    /// </summary>
    [Fact]
    public void Sha384_ForAbc_MatchesPublishedVector()
    {
        Assert.Equal(
            "CB00753F45A35E8BB5A03D699AC65007272C32AB0EDED1631A8B605A43FF5BED8086072BA1E7CC2358BAECA134C825A7",
            HashHelper.Sha384("abc"));
    }

    /// <summary>
    /// SHA512 固定向量
    /// </summary>
    [Fact]
    public void Sha512_ForAbc_MatchesPublishedVector()
    {
        Assert.Equal(
            "DDAF35A193617ABACC417349AE20413112E6FA4E89A97EA20A9EEEE64B55D39A2192992A274FC1A836BA3C23A3FEEBBD" +
            "454D4423643CE80E2A9AC94FA54CA49F",
            HashHelper.Sha512("abc"));
    }

    /// <summary>
    /// 所有摘要方法输出的都是大写十六进制且长度固定
    /// </summary>
    [Fact]
    public void AllDigests_AreUppercaseHexWithFixedLength()
    {
        var digests = new[]
        {
            (Value: HashHelper.Md5(ChineseSample), Length: 32),
            (Value: HashHelper.Sha1(ChineseSample), Length: 40),
            (Value: HashHelper.Sha256(ChineseSample), Length: 64),
            (Value: HashHelper.Sha384(ChineseSample), Length: 96),
            (Value: HashHelper.Sha512(ChineseSample), Length: 128)
        };

        Assert.All(digests, item =>
        {
            Assert.Equal(item.Length, item.Value.Length);
            Assert.Matches("^[0-9A-F]+$", item.Value);
        });
    }

    /// <summary>
    /// 中文按 UTF-8 编码后取摘要，与直接对 UTF-8 字节取摘要一致
    /// </summary>
    /// <remarks>
    /// 编码口径漂移是这类工具最隐蔽的破坏方式：换成 UTF-16 后同一段中文的摘要会完全不同，
    /// 但英文用例照样全绿。所以必须显式对比两种编码的结果。
    /// </remarks>
    [Fact]
    public void Md5_ForChinese_UsesUtf8Encoding()
    {
        var utf8Digest = HashHelper.ByteMd5(Encoding.UTF8.GetBytes(ChineseSample));
        var utf16Digest = HashHelper.ByteMd5(Encoding.Unicode.GetBytes(ChineseSample));

        Assert.Equal(utf8Digest, HashHelper.Md5(ChineseSample));
        Assert.NotEqual(utf16Digest, HashHelper.Md5(ChineseSample));
    }

    /// <summary>
    /// SHA256 同样按 UTF-8 编码中文
    /// </summary>
    [Fact]
    public void Sha256_ForChinese_UsesUtf8Encoding()
    {
        Assert.Equal(
            HashHelper.ByteHash(Encoding.UTF8.GetBytes(ChineseSample)),
            HashHelper.Sha256(ChineseSample));
    }

    /// <summary>
    /// 字节重载与字符串重载对同一份数据给出同一摘要
    /// </summary>
    [Fact]
    public void ByteOverloads_MatchStringOverloads()
    {
        var bytes = Encoding.UTF8.GetBytes("abc");

        Assert.Equal(HashHelper.Md5("abc"), HashHelper.ByteMd5(bytes));
        Assert.Equal(HashHelper.Sha256("abc"), HashHelper.ByteHash(bytes));
    }

    /// <summary>
    /// 空字节数组的摘要与空字符串一致
    /// </summary>
    [Fact]
    public void ByteMd5_ForEmptyArray_MatchesEmptyStringVector()
    {
        Assert.Equal("D41D8CD98F00B204E9800998ECF8427E", HashHelper.ByteMd5([]));
    }

    /// <summary>
    /// 流重载与字节重载对同一份数据给出同一摘要
    /// </summary>
    [Fact]
    public void StreamOverloads_MatchByteOverloads()
    {
        var bytes = Encoding.UTF8.GetBytes(ChineseSample);

        using var md5Stream = new MemoryStream(bytes);
        Assert.Equal(HashHelper.ByteMd5(bytes), HashHelper.StreamMd5(md5Stream));

        using var shaStream = new MemoryStream(bytes);
        Assert.Equal(HashHelper.ByteHash(bytes), HashHelper.StreamHash(shaStream));
    }

    /// <summary>
    /// 文件路径重载读取磁盘内容后给出与内存字节一致的摘要
    /// </summary>
    [Fact]
    public void StreamMd5_ForFilePath_MatchesContentDigest()
    {
        var filePath = Path.Combine(_tempDirectory, "content.txt");
        var bytes = Encoding.UTF8.GetBytes(ChineseSample);
        File.WriteAllBytes(filePath, bytes);

        Assert.Equal(HashHelper.ByteMd5(bytes), HashHelper.StreamMd5(filePath));
    }

    /// <summary>
    /// 文件不存在时抛出文件未找到异常
    /// </summary>
    [Fact]
    public void StreamMd5_WhenFileMissing_ThrowsFileNotFound()
    {
        var missingPath = Path.Combine(_tempDirectory, "not-exists.bin");

        Assert.Throws<FileNotFoundException>(() => { _ = HashHelper.StreamMd5(missingPath); });
    }

    /// <summary>
    /// 超长输入不会截断：不同长度的长文本摘要互不相同且长度恒定
    /// </summary>
    [Fact]
    public void Sha256_ForVeryLongInput_IsStableAndDistinct()
    {
        var long1 = new string('x', 1_000_000);
        var long2 = long1 + "y";

        var digest1 = HashHelper.Sha256(long1);
        var digest2 = HashHelper.Sha256(long2);

        Assert.Equal(64, digest1.Length);
        Assert.Equal(64, digest2.Length);
        Assert.NotEqual(digest1, digest2);
        Assert.Equal(digest1, HashHelper.Sha256(long1));
    }

    /// <summary>
    /// 单比特差异导致摘要完全不同（雪崩效应）
    /// </summary>
    [Fact]
    public void Sha256_ForNearlyIdenticalInputs_ProducesDifferentDigests()
    {
        Assert.NotEqual(HashHelper.Sha256("abc"), HashHelper.Sha256("abd"));
    }

    /// <summary>
    /// 释放临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (IOException)
        {
            // 临时目录清理失败不应影响测试结论
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }

        GC.SuppressFinalize(this);
    }
}
