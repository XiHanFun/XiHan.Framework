using System.Text;
using System.Text.Json;
using XiHan.Framework.Utils.Text.Json;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Test.Text.Json;

public class JsonHelperTest : IDisposable
{
    private readonly string _tempJsonFilePath;
    private readonly JsonHelper _jsonHelper;

    public JsonHelperTest()
    {
        // 创建临时 JSON 文件路径
        _tempJsonFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        // 初始化 JsonHelper
        _jsonHelper = new JsonHelper(_tempJsonFilePath);

        // 创建一个简单的测试 JSON 文件
        var testObject = new TestObject
        {
            Id = 1,
            Name = "测试",
            CreatedTime = DateTime.Now,
            Nested = new NestedObject
            {
                Value = "嵌套值"
            }
        };

        var json = JsonSerializer.Serialize(testObject, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
        File.WriteAllText(_tempJsonFilePath, json, Encoding.UTF8);
    }

    public void Dispose()
    {
        // 清理临时文件
        if (File.Exists(_tempJsonFilePath))
        {
            File.Delete(_tempJsonFilePath);
        }
    }

    [Fact]
    public void Get_ReturnsDeserializedObject()
    {
        // Act
        var result = _jsonHelper.Get<TestObject>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("测试", result.Name);
        Assert.NotNull(result.Nested);
        Assert.Equal("嵌套值", result.Nested.Value);
    }

    [Fact]
    public void Get_WithNonExistingFile_ReturnsDefault()
    {
        // Arrange
        var nonExistingFilePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
        var helper = new JsonHelper(nonExistingFilePath);

        // Act
        var result = helper.Get<TestObject>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Get_WithKeyLink_ReturnsNestedObject()
    {
        // Act
        var result = _jsonHelper.Get<NestedObject>("Nested");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("嵌套值", result.Value);
    }

    [Fact]
    public void Get_WithInvalidKeyLink_ReturnsDefault()
    {
        // Act
        var result = _jsonHelper.Get<NestedObject>("InvalidKey");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_UpdatesExistingValue()
    {
        // Arrange
        var newName = "更新后的名称";

        // Act
        _jsonHelper.Set<TestObject, string>("Name", newName);
        var result = _jsonHelper.Get<TestObject>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newName, result.Name);
    }

    [Fact]
    public void Set_CreatesNonExistingNestedObject()
    {
        // Arrange
        var newNestedObject = new NestedObject { Value = "新嵌套值" };

        // Act
        _jsonHelper.Set<TestObject, NestedObject>("NewNested", newNestedObject);
        var result = _jsonHelper.Get<TestObject>();

        // Assert
        Assert.NotNull(result);
        // 需要检查字段是否被添加，但由于 JsonHelper 的实现方式可能不会直接映射到主对象
        // 我们可以通过读取文件内容并检查是否包含新值来验证
        var fileContent = File.ReadAllText(_tempJsonFilePath);
        Assert.Contains("新嵌套值", fileContent);
    }

    // 测试用类
    private class TestObject
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public NestedObject? Nested { get; init; }
    }

    private class NestedObject
    {
        public string Value { get; init; } = string.Empty;
    }
}
