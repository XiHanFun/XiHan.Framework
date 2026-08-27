// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonHelper 编码一致性测试
/// </summary>
/// <remarks>
/// 覆盖两处编码口径不一致的修复：
/// 一是 FormatJson / CompressJson / CloneJson 内部新建的选项没有设置 Encoder，走默认严格编码器把中文转义成 \uXXXX，
/// 与同一个 Helper 里 Serialize 默认的 UnsafeRelaxedJsonEscaping 不一致；
/// 二是落盘用 JsonSerializeOptions.Encoding、读回却硬编码 UTF-8，不带前导码的非 UTF-8 文件读不回来。
/// 所有落盘都限制在进程临时目录下的独立随机子目录内，Dispose 时递归清理。
/// </remarks>
public class JsonHelperEncodingConsistencyTests : IDisposable
{
    private readonly string _rootDirectory;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public JsonHelperEncodingConsistencyTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 格式化不转义中文，且与原文结构等价
    /// </summary>
    [Fact]
    public void FormatJson_KeepsChineseUnescaped()
    {
        const string Source = "{\"a\":1,\"b\":{\"c\":\"文本\"}}";

        var formatted = JsonHelper.FormatJson(Source);

        Assert.Contains("文本", formatted);
        Assert.DoesNotContain("\\u", formatted);
        Assert.True(JsonHelper.CompareJson(Source, formatted));
    }

    /// <summary>
    /// 压缩不转义中文
    /// </summary>
    [Fact]
    public void CompressJson_KeepsChineseUnescaped()
    {
        var compressed = JsonHelper.CompressJson("{\n  \"name\" : \"曦寒\"\n}");

        Assert.Equal("{\"name\":\"曦寒\"}", compressed);
    }

    /// <summary>
    /// 克隆不转义中文
    /// </summary>
    [Fact]
    public void CloneJson_KeepsChineseUnescaped()
    {
        Assert.Equal("{\"name\":\"曦寒\"}", JsonHelper.CloneJson("{ \"name\" : \"曦寒\" }"));
    }

    /// <summary>
    /// 三条辅助路径与 Serialize 默认输出对中文的处理一致
    /// </summary>
    /// <remarks>
    /// 这是本组的核心断言：同一个 Helper 的两条路径不能对同一份中文给出不同文本。
    /// </remarks>
    [Fact]
    public void FormatCompressClone_AgreeWithSerializeOnChinese()
    {
        var serialized = JsonHelper.Serialize(
            new JsonSampleAddress { City = "上海", Country = "中国" },
            new JsonSerializeOptions { WriteIndented = false });

        Assert.Equal(serialized, JsonHelper.CompressJson(serialized));
        Assert.Equal(serialized, JsonHelper.CloneJson(serialized));
        Assert.Equal(serialized, JsonHelper.FormatJson(serialized, false));
    }

    /// <summary>
    /// 带前导码的编码写出的文件，默认重载靠 BOM 探测就能读回
    /// </summary>
    [Fact]
    public void SerializeToFile_WithBomCarryingEncoding_RoundTripsWithDefaultRead()
    {
        var path = Path.Combine(_rootDirectory, "utf16.json");
        var options = new JsonSerializeOptions { WriteIndented = false, Encoding = Encoding.Unicode };

        JsonHelper.SerializeToFile(new JsonSampleUser { Name = "曦寒", Age = 18 }, path, options);
        var restored = JsonHelper.DeserializeFromFile<JsonSampleUser>(path);

        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(18, restored.Age);
    }

    /// <summary>
    /// 不带前导码的非 UTF-8 编码必须由读取端显式指定同一个编码才能无损读回
    /// </summary>
    /// <remarks>
    /// 修复前读取端硬编码 Encoding.UTF8，这类文件读回来是替换字符；
    /// 现在传入与写入相同的编码即可闭环。
    /// </remarks>
    [Fact]
    public void DeserializeFromFile_WithMatchingEncoding_RoundTripsBomlessEncoding()
    {
        var path = Path.Combine(_rootDirectory, "latin1.json");
        var options = new JsonSerializeOptions { WriteIndented = false, Encoding = Encoding.Latin1 };

        JsonHelper.SerializeToFile(new JsonSampleUser { Name = "café", Age = 7 }, path, options);

        var byMatchingEncoding = JsonHelper.DeserializeFromFile<JsonSampleUser>(path, Encoding.Latin1);
        Assert.Equal("café", byMatchingEncoding.Name);
        Assert.Equal(7, byMatchingEncoding.Age);

        // 反例：按默认的 UTF-8 读同一个文件，0xE9 不是合法 UTF-8 序列，会被替换掉
        var byDefaultEncoding = JsonHelper.DeserializeFromFile<JsonSampleUser>(path);
        Assert.NotEqual("café", byDefaultEncoding.Name);
    }

    /// <summary>
    /// 显式重载不传编码时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void DeserializeFromFile_WhenEncodingNull_ThrowsArgumentNullException()
    {
        var path = Path.Combine(_rootDirectory, "encoding-null.json");
        JsonHelper.SerializeToFile(new JsonSampleUser { Name = "曦寒" }, path);

        Assert.Throws<ArgumentNullException>(() =>
        {
            JsonHelper.DeserializeFromFile<JsonSampleUser>(path, (Encoding)null!);
        });
    }

    /// <summary>
    /// 显式重载同样在文件缺失时抛出 FileNotFoundException
    /// </summary>
    [Fact]
    public void DeserializeFromFile_WithEncoding_WhenFileMissing_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            JsonHelper.DeserializeFromFile<JsonSampleUser>(Path.Combine(_rootDirectory, "缺失.json"), Encoding.UTF8);
        });
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
