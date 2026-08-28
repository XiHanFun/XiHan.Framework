// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonHelper 文件读写测试
/// </summary>
/// <remarks>
/// 所有落盘都限制在进程临时目录下的独立随机子目录内，Dispose 时递归清理，
/// 保证并行执行的其它测试类不会互相踩到文件。
/// </remarks>
public class JsonHelperFileTests : IDisposable
{
    private readonly string _rootDirectory;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public JsonHelperFileTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 写文件再读回，中文与嵌套结构均保持一致
    /// </summary>
    [Fact]
    public void SerializeToFile_ThenDeserializeFromFile_RoundTrips()
    {
        var path = Path.Combine(_rootDirectory, "user.json");
        var source = new JsonSampleUser
        {
            Name = "曦寒",
            Age = 18,
            IsActive = true,
            Tags = ["框架", "工具库"],
            Address = new JsonSampleAddress { City = "上海", Country = "中国" }
        };

        JsonHelper.SerializeToFile(source, path);
        var restored = JsonHelper.DeserializeFromFile<JsonSampleUser>(path);

        Assert.True(File.Exists(path));
        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(18, restored.Age);
        Assert.Equal(source.Tags, restored.Tags);
        Assert.NotNull(restored.Address);
        Assert.Equal("上海", restored.Address!.City);
    }

    /// <summary>
    /// 目标目录不存在时自动创建
    /// </summary>
    [Fact]
    public void SerializeToFile_WhenDirectoryMissing_CreatesIt()
    {
        var path = Path.Combine(_rootDirectory, "深层", "目录", "user.json");

        JsonHelper.SerializeToFile(new JsonSampleUser { Name = "曦寒" }, path);

        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// 文件不存在时反序列化抛出 FileNotFoundException
    /// </summary>
    [Fact]
    public void DeserializeFromFile_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_rootDirectory, "缺失.json");

        Assert.Throws<FileNotFoundException>(() =>
        {
            JsonHelper.DeserializeFromFile<JsonSampleUser>(path);
        });
    }

    /// <summary>
    /// 文件不存在时 TryDeserializeFromFile 返回 false
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenFileMissing_ReturnsFalse()
    {
        var succeeded = JsonHelper.TryDeserializeFromFile<JsonSampleUser>(
            Path.Combine(_rootDirectory, "缺失.json"),
            out var user);

        Assert.False(succeeded);
        Assert.Null(user);
    }

    /// <summary>
    /// 文件内容非法时 TryDeserializeFromFile 返回 false
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenContentInvalid_ReturnsFalse()
    {
        var path = Path.Combine(_rootDirectory, "损坏.json");
        File.WriteAllText(path, "{不是 JSON");

        var succeeded = JsonHelper.TryDeserializeFromFile<JsonSampleUser>(path, out var user);

        Assert.False(succeeded);
        Assert.Null(user);
    }

    /// <summary>
    /// 文件内容合法时 TryDeserializeFromFile 返回 true
    /// </summary>
    [Fact]
    public void TryDeserializeFromFile_WhenContentValid_ReturnsTrue()
    {
        var path = Path.Combine(_rootDirectory, "正常.json");
        JsonHelper.SerializeToFile(new JsonSampleUser { Name = "曦寒", Age = 20 }, path);

        var succeeded = JsonHelper.TryDeserializeFromFile<JsonSampleUser>(path, out var user);

        Assert.True(succeeded);
        Assert.NotNull(user);
        Assert.Equal("曦寒", user!.Name);
        Assert.Equal(20, user.Age);
    }

    /// <summary>
    /// TrySerializeToFile 正常写入返回 true
    /// </summary>
    [Fact]
    public void TrySerializeToFile_WithValidObject_ReturnsTrue()
    {
        var path = Path.Combine(_rootDirectory, "try.json");

        var succeeded = JsonHelper.TrySerializeToFile(new JsonSampleUser { Name = "曦寒" }, path);

        Assert.True(succeeded);
        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// TrySerializeToFile 遇到 null 返回 false 且不落盘
    /// </summary>
    [Fact]
    public void TrySerializeToFile_WhenObjectNull_ReturnsFalseAndWritesNothing()
    {
        var path = Path.Combine(_rootDirectory, "null.json");

        var succeeded = JsonHelper.TrySerializeToFile<JsonSampleUser?>(null, path);

        Assert.False(succeeded);
        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// 落盘的文件本身是合法 JSON，可被独立校验
    /// </summary>
    [Fact]
    public void SerializeToFile_WritesValidJsonContent()
    {
        var path = Path.Combine(_rootDirectory, "content.json");

        JsonHelper.SerializeToFile(new JsonSampleUser { Name = "曦寒" }, path);
        var content = File.ReadAllText(path);

        Assert.True(JsonHelper.IsValidJson(content));
        Assert.Contains("曦寒", content);
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
