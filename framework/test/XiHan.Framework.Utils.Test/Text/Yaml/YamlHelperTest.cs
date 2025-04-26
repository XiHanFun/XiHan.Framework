using XiHan.Framework.Utils.Text.Yaml;

namespace XiHan.Framework.Utils.Test.Text.Yaml;

public class YamlHelperTest : IDisposable
{
    private readonly string _tempYamlFilePath;

    public YamlHelperTest()
    {
        // 创建临时测试文件路径
        _tempYamlFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.yaml");
    }

    public void Dispose()
    {
        // 测试结束后清理临时文件
        if (File.Exists(_tempYamlFilePath))
        {
            File.Delete(_tempYamlFilePath);
        }
    }

    [Fact]
    public void ParseYaml_BasicKeyValues_ReturnsCorrectDictionary()
    {
        // Arrange
        var yaml = @"
name: 测试用户
age: 25
isActive: true
score: 98.5
";

        // Act
        var result = YamlHelper.ParseYaml(yaml);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("测试用户", result["name"]);
        Assert.Equal("25", result["age"]);
        Assert.Equal("true", result["isActive"]);
        Assert.Equal("98.5", result["score"]);
    }

    [Fact]
    public void ParseYaml_WithComments_IgnoresComments()
    {
        // Arrange
        var yaml = @"
# 这是一个注释
name: 测试用户 # 行内注释
# 这是另一个注释
age: 25
";

        // Act
        var result = YamlHelper.ParseYaml(yaml);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("测试用户", result["name"]);
        Assert.Equal("25", result["age"]);
    }

    [Fact]
    public void ParseYaml_WithQuotedValues_HandlesQuotes()
    {
        // Arrange
        var yaml = @"
name: ""包含:特殊字符""
description: '这是一个''描述'
";

        // Act
        var result = YamlHelper.ParseYaml(yaml);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("包含:特殊字符", result["name"]);
        Assert.Equal("这是一个'描述", result["description"]);
    }

    [Fact]
    public void ConvertToYaml_BasicKeyValues_GeneratesCorrectYaml()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["name"] = "测试用户",
            ["age"] = "25",
            ["score"] = "98.5"
        };

        // Act
        var yaml = YamlHelper.ConvertToYaml(data);
        var parsed = YamlHelper.ParseYaml(yaml);

        // Assert
        Assert.Equal(data.Count, parsed.Count);
        foreach (var kvp in data)
        {
            Assert.Equal(kvp.Value, parsed[kvp.Key]);
        }
    }

    [Fact]
    public void ConvertToYaml_WithSpecialChars_AddsQuotes()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["name"] = "包含:冒号",
            ["list"] = "1, 2, 3",
            ["empty"] = ""
        };

        // Act
        var yaml = YamlHelper.ConvertToYaml(data);

        // Assert
        Assert.Contains("name: \"包含:冒号\"", yaml);
        Assert.Contains("empty: \"\"", yaml);
    }

    [Fact]
    public void SaveAndLoadFromFile_RoundTrip_WorksCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["name"] = "测试用户",
            ["age"] = "25",
            ["isActive"] = "true",
            ["special"] = "需要\"引号\"的内容"
        };

        // Act
        YamlHelper.SaveToFile(_tempYamlFilePath, data);
        var loadedData = YamlHelper.LoadFromFile(_tempYamlFilePath);

        // Assert
        Assert.Equal(data.Count, loadedData.Count);
        foreach (var kvp in data)
        {
            Assert.Equal(kvp.Value, loadedData[kvp.Key]);
        }
    }

    [Fact]
    public void ParseNestedYaml_HandlesHierarchy_ReturnsCorrectlyFlattened()
    {
        // Arrange
        var yaml = @"
server:
  host: localhost
  port: 8080
  ssl: true
database:
  connectionString: ""Server=db;Database=test""
  timeout: 30
logging:
  level: debug
  file: logs.txt
";

        // Act
        var result = YamlHelper.ParseNestedYaml(yaml);

        // Assert
        Assert.Equal(7, result.Count);
        Assert.Equal("localhost", result["server.host"]);
        Assert.Equal("8080", result["server.port"]);
        Assert.Equal("true", result["server.ssl"]);
        Assert.Equal("Server=db;Database=test", result["database.connectionString"]);
        Assert.Equal("30", result["database.timeout"]);
        Assert.Equal("debug", result["logging.level"]);
        Assert.Equal("logs.txt", result["logging.file"]);
    }

    [Fact]
    public void ParseNestedYaml_WithMultipleLevels_HandlesCorrectly()
    {
        // Arrange
        var yaml = @"
application:
  api:
    version: v1
    endpoints:
      users: /api/users
      products: /api/products
  web:
    domain: example.com
";

        // Act
        var result = YamlHelper.ParseNestedYaml(yaml);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal("v1", result["application.api.version"]);
        Assert.Equal("/api/users", result["application.api.endpoints.users"]);
        Assert.Equal("/api/products", result["application.api.endpoints.products"]);
        Assert.Equal("example.com", result["application.web.domain"]);
    }
}
