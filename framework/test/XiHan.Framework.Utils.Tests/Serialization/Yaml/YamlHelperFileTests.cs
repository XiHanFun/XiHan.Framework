// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlHelper 文件读写测试
/// </summary>
/// <remarks>
/// 字典型接口（LoadFromFile）与对象型接口（DeserializeFromFile）对"文件不存在"的处理不同：
/// 前者返回空字典，后者抛 FileNotFoundException，这个差异是对外契约，逐个锁定。
/// </remarks>
public class YamlHelperFileTests : IDisposable
{
    private readonly string _rootDirectory;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public YamlHelperFileTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 字典存盘再读回保持一致，含中文与特殊字符
    /// </summary>
    [Fact]
    public void SaveToFile_ThenLoadFromFile_RoundTrips()
    {
        var path = Path.Combine(_rootDirectory, "config.yaml");
        var source = new Dictionary<string, string>
        {
            ["name"] = "曦寒",
            ["url"] = "http://example.com",
            ["port"] = "8080"
        };

        YamlHelper.SaveToFile(path, source);
        var restored = YamlHelper.LoadFromFile(path);

        Assert.True(File.Exists(path));
        Assert.Equal(source.Count, restored.Count);
        Assert.Equal("曦寒", restored["name"]);
        Assert.Equal("http://example.com", restored["url"]);
        Assert.Equal("8080", restored["port"]);
    }

    /// <summary>
    /// 目标目录不存在时自动创建
    /// </summary>
    [Fact]
    public void SaveToFile_WhenDirectoryMissing_CreatesIt()
    {
        var path = Path.Combine(_rootDirectory, "深层", "目录", "config.yaml");

        YamlHelper.SaveToFile(path, new Dictionary<string, string> { ["name"] = "曦寒" });

        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// 文件不存在时字典加载返回空字典而不是抛异常
    /// </summary>
    [Fact]
    public void LoadFromFile_WhenFileMissing_ReturnsEmptyDictionary()
    {
        Assert.Empty(YamlHelper.LoadFromFile(Path.Combine(_rootDirectory, "缺失.yaml")));
    }

    /// <summary>
    /// TryLoadFromFile 在文件缺失与存在时的返回值
    /// </summary>
    [Fact]
    public void TryLoadFromFile_ReflectsFileExistence()
    {
        var missing = YamlHelper.TryLoadFromFile(Path.Combine(_rootDirectory, "缺失.yaml"), out var empty);
        Assert.False(missing);
        Assert.Empty(empty);

        var path = Path.Combine(_rootDirectory, "存在.yaml");
        YamlHelper.SaveToFile(path, new Dictionary<string, string> { ["name"] = "曦寒" });

        var found = YamlHelper.TryLoadFromFile(path, out var loaded);
        Assert.True(found);
        Assert.Equal("曦寒", loaded["name"]);
    }

    /// <summary>
    /// TrySaveToFile 正常写入返回 true
    /// </summary>
    [Fact]
    public void TrySaveToFile_WithValidPath_ReturnsTrue()
    {
        var path = Path.Combine(_rootDirectory, "try.yaml");

        var succeeded = YamlHelper.TrySaveToFile(path, new Dictionary<string, string> { ["name"] = "曦寒" });

        Assert.True(succeeded);
        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// 对象存盘再读回保持一致
    /// </summary>
    [Fact]
    public void SerializeToFile_ThenDeserializeFromFile_RoundTrips()
    {
        var path = Path.Combine(_rootDirectory, "app.yaml");
        var source = new YamlSampleConfig
        {
            Name = "曦寒",
            Enabled = true,
            Server = new YamlSampleServer { Host = "localhost", Port = 8080 }
        };

        YamlHelper.SerializeToFile(source, path);
        var restored = YamlHelper.DeserializeFromFile<YamlSampleConfig>(path);

        Assert.Equal("曦寒", restored.Name);
        Assert.True(restored.Enabled);
        Assert.Equal("localhost", restored.Server.Host);
        Assert.Equal(8080, restored.Server.Port);
    }

    /// <summary>
    /// 文件不存在时对象反序列化抛出 FileNotFoundException
    /// </summary>
    [Fact]
    public void DeserializeFromFile_WhenFileMissing_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            YamlHelper.DeserializeFromFile<YamlSampleConfig>(Path.Combine(_rootDirectory, "缺失.yaml"));
        });
    }

    /// <summary>
    /// 文件不存在时 TryDeserializeFromFile 返回 false
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenFileMissing_ReturnsFalse()
    {
        var succeeded = YamlHelper.TryDeserializeFromFile<YamlSampleConfig>(
            Path.Combine(_rootDirectory, "缺失.yaml"),
            out var config);

        Assert.False(succeeded);
        Assert.Null(config);
    }

    /// <summary>
    /// 文件存在时 TryDeserializeFromFile 返回 true
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenFileValid_ReturnsTrue()
    {
        var path = Path.Combine(_rootDirectory, "正常.yaml");
        YamlHelper.SerializeToFile(new YamlSampleConfig { Name = "曦寒" }, path);

        var succeeded = YamlHelper.TryDeserializeFromFile<YamlSampleConfig>(path, out var config);

        Assert.True(succeeded);
        Assert.NotNull(config);
        Assert.Equal("曦寒", config!.Name);
    }

    /// <summary>
    /// TrySerializeToFile 正常写入返回 true，遇到 null 返回 false 且不落盘
    /// </summary>
    [Fact]
    public void TrySerializeToFile_ReflectsObjectNullability()
    {
        var validPath = Path.Combine(_rootDirectory, "try-object.yaml");
        var nullPath = Path.Combine(_rootDirectory, "try-null.yaml");

        Assert.True(YamlHelper.TrySerializeToFile(new YamlSampleConfig { Name = "曦寒" }, validPath));
        Assert.True(File.Exists(validPath));

        Assert.False(YamlHelper.TrySerializeToFile<YamlSampleConfig?>(null, nullPath));
        Assert.False(File.Exists(nullPath));
    }

    /// <summary>
    /// 落盘内容为 UTF-8，中文读回不乱码
    /// </summary>
    [Fact]
    public void SaveToFile_WritesUtf8Content()
    {
        var path = Path.Combine(_rootDirectory, "encoding.yaml");

        YamlHelper.SaveToFile(path, new Dictionary<string, string> { ["name"] = "曦寒框架" });
        var content = File.ReadAllText(path);

        Assert.Contains("name: 曦寒框架", content);
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
