// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XmlHelper 文件读写测试
/// </summary>
/// <remarks>
/// 落盘一律使用紧凑选项（不写 XML 声明）：声明里的 encoding 由底层 TextWriter 决定，
/// 与实际落盘编码不是一回事，不应把它卷进往返断言。
/// </remarks>
public class XmlHelperFileTests : IDisposable
{
    private readonly string _rootDirectory;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public XmlHelperFileTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 写文件再读回，中文保持一致
    /// </summary>
    [Fact]
    public void SerializeToFile_ThenDeserializeFromFile_RoundTrips()
    {
        var path = Path.Combine(_rootDirectory, "person.xml");
        var source = new XmlTestPerson { Name = "曦寒", Age = 18 };

        XmlHelper.SerializeToFile(source, path, XmlSerializeOptions.Compact);
        var restored = XmlHelper.DeserializeFromFile<XmlTestPerson>(path);

        Assert.True(File.Exists(path));
        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(18, restored.Age);
    }

    /// <summary>
    /// 目标目录不存在时自动创建
    /// </summary>
    [Fact]
    public void SerializeToFile_WhenDirectoryMissing_CreatesIt()
    {
        var path = Path.Combine(_rootDirectory, "深层", "目录", "person.xml");

        XmlHelper.SerializeToFile(new XmlTestPerson { Name = "曦寒" }, path, XmlSerializeOptions.Compact);

        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// 落盘内容为合法 XML 且中文不乱码
    /// </summary>
    [Fact]
    public void SerializeToFile_WritesValidUtf8Content()
    {
        var path = Path.Combine(_rootDirectory, "content.xml");

        XmlHelper.SerializeToFile(new XmlTestPerson { Name = "曦寒" }, path, XmlSerializeOptions.Compact);
        var content = File.ReadAllText(path);

        Assert.True(XmlHelper.IsValidXml(content));
        Assert.Contains("<Name>曦寒</Name>", content);
    }

    /// <summary>
    /// 文件不存在时反序列化抛出 FileNotFoundException
    /// </summary>
    [Fact]
    public void DeserializeFromFile_WhenFileMissing_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            XmlHelper.DeserializeFromFile<XmlTestPerson>(Path.Combine(_rootDirectory, "缺失.xml"));
        });
    }

    /// <summary>
    /// 文件不存在时 TryDeserializeFromFile 返回 false
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenFileMissing_ReturnsFalse()
    {
        var succeeded = XmlHelper.TryDeserializeFromFile<XmlTestPerson>(
            Path.Combine(_rootDirectory, "缺失.xml"),
            out var person);

        Assert.False(succeeded);
        Assert.Null(person);
    }

    /// <summary>
    /// 文件内容损坏时 TryDeserializeFromFile 返回 false
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenContentBroken_ReturnsFalse()
    {
        var path = Path.Combine(_rootDirectory, "损坏.xml");
        File.WriteAllText(path, "<XmlTestPerson><Name>曦寒");

        var succeeded = XmlHelper.TryDeserializeFromFile<XmlTestPerson>(path, out var person);

        Assert.False(succeeded);
        Assert.Null(person);
    }

    /// <summary>
    /// 文件内容合法时 TryDeserializeFromFile 返回 true
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenContentValid_ReturnsTrue()
    {
        var path = Path.Combine(_rootDirectory, "正常.xml");
        XmlHelper.SerializeToFile(new XmlTestPerson { Name = "曦寒", Age = 20 }, path, XmlSerializeOptions.Compact);

        var succeeded = XmlHelper.TryDeserializeFromFile<XmlTestPerson>(path, out var person);

        Assert.True(succeeded);
        Assert.NotNull(person);
        Assert.Equal(20, person!.Age);
    }

    /// <summary>
    /// TrySerializeToFile 正常写入返回 true
    /// </summary>
    [Fact]
    public void TrySerializeToFile_WithValidObject_ReturnsTrue()
    {
        var path = Path.Combine(_rootDirectory, "try.xml");

        var succeeded = XmlHelper.TrySerializeToFile(new XmlTestPerson { Name = "曦寒" }, path, XmlSerializeOptions.Compact);

        Assert.True(succeeded);
        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// TrySerializeToFile 遇到 null 返回 false 且不落盘
    /// </summary>
    [Fact]
    public void TrySerializeToFile_WhenObjectNull_ReturnsFalseAndWritesNothing()
    {
        var path = Path.Combine(_rootDirectory, "null.xml");

        var succeeded = XmlHelper.TrySerializeToFile<XmlTestPerson?>(null, path);

        Assert.False(succeeded);
        Assert.False(File.Exists(path));
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
