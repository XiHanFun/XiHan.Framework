// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XmlHelper 编码一致性测试
/// </summary>
/// <remarks>
/// 修复前有两处对不上：Serialize 把 options.Encoding 交给 XmlWriterSettings，
/// 但写入目标是 StringWriter，声明恒为 encoding="utf-16"；SerializeToFile 又硬编码 UTF-8 落盘。
/// 结果是声明与文件字节不是一个编码，第三方工具按声明解析就会乱码。
/// 这里锁死"声明编码 = 选项编码 = 落盘编码"。
/// </remarks>
public class XmlHelperEncodingTests : IDisposable
{
    private readonly string _rootDirectory;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public XmlHelperEncodingTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 默认选项的 XML 声明为 utf-8
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultOptions_DeclaresUtf8()
    {
        var xml = XmlHelper.Serialize(new XmlTestPerson { Name = "曦寒", Age = 18 });

        Assert.Contains("encoding=\"utf-8\"", xml);
        Assert.DoesNotContain("encoding=\"utf-16\"", xml);
    }

    /// <summary>
    /// XML 声明跟随选项里的编码
    /// </summary>
    [Fact]
    public void Serialize_WithUnicodeEncoding_DeclaresUtf16()
    {
        var xml = XmlHelper.Serialize(
            new XmlTestPerson { Name = "曦寒" },
            new XmlSerializeOptions { Encoding = Encoding.Unicode });

        Assert.Contains("encoding=\"utf-16\"", xml);
    }

    /// <summary>
    /// 落盘编码跟随选项，声明与实际字节一致
    /// </summary>
    [Fact]
    public void SerializeToFile_WithUnicodeEncoding_WritesUtf16Bytes()
    {
        var path = Path.Combine(_rootDirectory, "unicode.xml");

        XmlHelper.SerializeToFile(
            new XmlTestPerson { Name = "曦寒", Age = 18 },
            path,
            new XmlSerializeOptions { Encoding = Encoding.Unicode });

        var bytes = File.ReadAllBytes(path);
        var text = File.ReadAllText(path, Encoding.Unicode);

        // UTF-16 落盘后 ASCII 字符会带上 0 字节，UTF-8 不会，用它区分实际落盘编码
        Assert.Contains((byte)0, bytes);
        Assert.Contains("encoding=\"utf-16\"", text);
        Assert.Contains("<Name>曦寒</Name>", text);
    }

    /// <summary>
    /// 未指定编码时仍按 UTF-8 落盘
    /// </summary>
    [Fact]
    public void SerializeToFile_WithDefaultOptions_StillWritesUtf8Bytes()
    {
        var path = Path.Combine(_rootDirectory, "utf8.xml");

        XmlHelper.SerializeToFile(new XmlTestPerson { Name = "曦寒", Age = 18 }, path);

        var bytes = File.ReadAllBytes(path);
        var text = File.ReadAllText(path);

        Assert.DoesNotContain((byte)0, bytes);
        Assert.Contains("encoding=\"utf-8\"", text);
        Assert.Contains("<Name>曦寒</Name>", text);
    }

    /// <summary>
    /// 按 UTF-8 落盘的文件能被同一套 API 读回
    /// </summary>
    [Fact]
    public void SerializeToFile_ThenDeserializeFromFile_WithDefaultEncoding_RoundTrips()
    {
        var path = Path.Combine(_rootDirectory, "roundtrip.xml");

        XmlHelper.SerializeToFile(new XmlTestPerson { Name = "曦寒", Age = 18 }, path);
        var restored = XmlHelper.DeserializeFromFile<XmlTestPerson>(path);

        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(18, restored.Age);
    }

    /// <summary>
    /// 省略声明时不受编码选项影响
    /// </summary>
    [Fact]
    public void Serialize_WhenDeclarationOmitted_WritesNoEncodingAttribute()
    {
        var xml = XmlHelper.Serialize(
            new XmlTestPerson { Name = "曦寒" },
            new XmlSerializeOptions { OmitXmlDeclaration = true, Encoding = Encoding.Unicode });

        Assert.DoesNotContain("<?xml", xml);
        Assert.DoesNotContain("encoding=", xml);
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, true);
            }
        }
        catch (Exception)
        {
            // 忽略清理异常
        }

        GC.SuppressFinalize(this);
    }
}
