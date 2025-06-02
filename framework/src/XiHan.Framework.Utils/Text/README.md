# XiHan.Framework.Utils.Text 使用指南

本文档介绍 XiHan.Framework.Utils.Text 命名空间下所有文本处理功能的使用方法，包括 XML、YAML、JSON 处理、模板引擎、字符串操作和编码转换等。

## 目录

- [JSON 功能](#json-功能)
- [XML 功能](#xml-功能)
- [YAML 功能](#yaml-功能)
- [模板引擎](#模板引擎)
- [字符串处理](#字符串处理)
- [编码转换](#编码转换)
- [最佳实践](#最佳实践)

## JSON 功能

### 基本序列化和反序列化

```csharp
using XiHan.Framework.Utils.Text.Json;

// 定义数据模型
public class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Email { get; set; } = "";
}

// 序列化对象为 JSON
var person = new Person { Name = "张三", Age = 30, Email = "zhangsan@example.com" };

// 基本序列化
string json = JsonHelper.Serialize(person);

// 使用自定义选项序列化
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
string formattedJson = JsonHelper.Serialize(person, options);

// 反序列化 JSON 为对象
Person? deserializedPerson = JsonHelper.Deserialize<Person>(json);

// 安全反序列化
if (JsonHelper.TryDeserialize<Person>(json, out Person? result))
{
    Console.WriteLine($"反序列化成功: {result.Name}");
}
```

### 异步操作

```csharp
// 异步流操作
using var stream = new MemoryStream();
await JsonHelper.SerializeAsync(stream, person);

stream.Position = 0;
Person? loadedPerson = await JsonHelper.DeserializeAsync<Person>(stream);
```

### 动态 JSON 解析

```csharp
string jsonText = @"{""name"":""张三"",""age"":30,""hobbies"":[""阅读"",""游泳""]}";

// 解析为动态对象
if (JsonHelper.TryParseJsonDynamic(jsonText, out dynamic? jsonObject))
{
    Console.WriteLine($"姓名: {jsonObject.name}");
    Console.WriteLine($"年龄: {jsonObject.age}");
}

// 解析为 JsonNode
JsonNode? node = JsonHelper.ParseToJsonNode(jsonText);
string? name = node?["name"]?.ToString();

// 解析为 JsonDocument
using JsonDocument? doc = JsonHelper.ParseToJsonDocument(jsonText);
if (doc?.RootElement.TryGetProperty("name", out JsonElement nameElement) == true)
{
    Console.WriteLine($"姓名: {nameElement.GetString()}");
}
```

### JSON 路径查询

```csharp
string complexJson = @"
{
    ""user"": {
        ""profile"": {
            ""name"": ""张三"",
            ""contacts"": {
                ""email"": ""zhangsan@example.com""
            }
        }
    }
}";

// 按路径获取值
string? name = JsonHelper.GetValueByPath<string>(complexJson, "user.profile.name");
string? email = JsonHelper.GetValueByPath<string>(complexJson, "user.profile.contacts.email");
```

### JSON 操作和工具

```csharp
// JSON 验证
bool isValid = JsonHelper.IsValidJson(jsonText);

// JSON 格式化
string formatted = JsonHelper.FormatJson(json, indented: true);

// JSON 压缩
string compressed = JsonHelper.CompressJson(formatted);

// JSON 合并
string json1 = @"{""name"":""张三"",""age"":30}";
string json2 = @"{""age"":31,""city"":""北京""}";
string merged = JsonHelper.MergeJson(json1, json2, overwriteExisting: true);

// JSON 比较
bool areEqual = JsonHelper.JsonEquals(json1, json2, ignoreOrder: true);

// 深拷贝
Person cloned = JsonHelper.DeepClone(person);

// 对象转换
var dict = JsonHelper.ConvertTo<Dictionary<string, object>>(person);
```

### JSON 扩展方法

```csharp
using XiHan.Framework.Utils.Text.Json;

// 对象扩展
string json = person.ToJson();
string formattedJson = person.ToJson(options);

// 字符串扩展
Person? person = json.FromJson<Person>();
bool isValid = json.IsValidJson();
string formatted = json.FormatJson();
string compressed = json.CompressJson();

// 异步扩展
string json = await person.ToJsonAsync();
Person? person = await json.FromJsonAsync<Person>();
```

## XML 功能

### 基本序列化和反序列化

```csharp
using XiHan.Framework.Utils.Text.Xml;

// 定义数据模型
public class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Email { get; set; } = "";
}

// 序列化对象为 XML
var person = new Person { Name = "张三", Age = 30, Email = "zhangsan@example.com" };

// 使用默认选项
string xml = XmlHelper.Serialize(person);

// 使用自定义选项
var options = new XmlSerializationOptions
{
    Indent = true,
    IndentChars = "    ",
    OmitXmlDeclaration = false
};
string formattedXml = XmlHelper.Serialize(person, options);

// 反序列化 XML 为对象
Person deserializedPerson = XmlHelper.Deserialize<Person>(xml);
```

### 异步操作

```csharp
// 异步序列化
string xml = await XmlHelper.SerializeAsync(person);

// 异步反序列化
Person person = await XmlHelper.DeserializeAsync<Person>(xml);

// 异步文件操作
await XmlHelper.SerializeToFileAsync(person, "person.xml");
Person loadedPerson = await XmlHelper.DeserializeFromFileAsync<Person>("person.xml");
```

### XML 节点操作

```csharp
string xmlContent = @"
<root>
    <person id='1'>
        <name>张三</name>
        <age>30</age>
    </person>
</root>";

// 查询节点内容
string name = XmlHelper.QueryNode(xmlContent, "//person/name"); // "张三"

// 查询节点属性
string id = XmlHelper.QueryNodeAttribute(xmlContent, "//person", "id"); // "1"

// 添加节点
string updatedXml = XmlHelper.AddNode(xmlContent, "//person", "email", "zhangsan@example.com");

// 修改节点值
string modifiedXml = XmlHelper.UpdateNode(xmlContent, "//person/age", "31");

// 删除节点
string removedXml = XmlHelper.RemoveNode(xmlContent, "//person/email");
```

### XML 验证

```csharp
// XSD 验证
string xsdSchema = @"<?xml version='1.0'?>
<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>
  <xs:element name='person'>
    <xs:complexType>
      <xs:sequence>
        <xs:element name='name' type='xs:string'/>
        <xs:element name='age' type='xs:int'/>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>";

bool isValid = XmlHelper.ValidateXmlWithXsd(xmlContent, xsdSchema, out List<string> errors);
```

### 扩展方法使用

```csharp
using XiHan.Framework.Utils.Text.Xml;

// 对象扩展
string xml = person.ToXml();
string formattedXml = person.ToXml(XmlSerializationOptions.Formatted);

// 字符串扩展
Person person = xml.FromXml<Person>();
bool isValid = xml.IsValidXml();
string formatted = xml.FormatXml();
Dictionary<string, string> dict = xml.ToDictionary();
```

## YAML 功能

### 基本序列化和反序列化

```csharp
using XiHan.Framework.Utils.Text.Yaml;

// 序列化对象为 YAML
var person = new Person { Name = "张三", Age = 30, Email = "zhangsan@example.com" };

// 使用默认选项
string yaml = YamlHelper.Serialize(person);

// 使用自定义选项
var options = new YamlSerializeOptions
{
    IndentSize = 4,
    IncludeDocumentMarkers = true,
    HeaderComment = "用户配置文件"
};
string formattedYaml = YamlHelper.Serialize(person, options);

// 反序列化 YAML 为对象
Person deserializedPerson = YamlHelper.Deserialize<Person>(yaml);
```

### 字典操作

```csharp
// 解析 YAML 为字典
string yamlContent = @"
database:
  host: localhost
  port: 5432
  name: mydb
server:
  host: 0.0.0.0
  port: 8080
";

// 简单解析
Dictionary<string, string> simpleDict = YamlHelper.ParseYaml(yamlContent);

// 嵌套解析（扁平化）
Dictionary<string, string> nestedDict = YamlHelper.ParseNestedYaml(yamlContent);
// 结果: {"database.host": "localhost", "database.port": "5432", ...}

// 字典转 YAML
string yamlFromDict = YamlHelper.ConvertToYaml(nestedDict);
```

### 文件操作

```csharp
// 从文件加载
Dictionary<string, string> config = YamlHelper.LoadFromFile("config.yaml");

// 保存到文件
YamlHelper.SaveToFile("output.yaml", config);

// 异步操作
var configAsync = await YamlHelper.LoadFromFileAsync("config.yaml");
await YamlHelper.SaveToFileAsync("output.yaml", config);

// 对象文件操作
await YamlHelper.SerializeToFileAsync(person, "person.yaml");
Person loadedPerson = await YamlHelper.DeserializeFromFileAsync<Person>("person.yaml");
```

### 格式转换

```csharp
// YAML 转 JSON
string json = YamlHelper.YamlToJson(yamlContent);

// JSON 转 YAML
string yaml = YamlHelper.JsonToYaml(json);

// 格式化 YAML
string formatted = YamlHelper.FormatYaml(yamlContent);
```

### 扩展方法使用

```csharp
using XiHan.Framework.Utils.Text.Yaml;

// 对象扩展
string yaml = person.ToYaml();
string formattedYaml = person.ToYaml(YamlSerializeOptions.Formatted);

// 字符串扩展
Person person = yaml.FromYaml<Person>();
bool isValid = yaml.IsValidYaml();
Dictionary<string, string> dict = yaml.ParseNestedYaml();
string json = yaml.ToJson();

// 字典扩展
string yaml = dict.ToYaml();
string value = dict.GetNestedValue("database.host", defaultValue: "localhost");
dict.SetNestedValue("database.timeout", "30");

// 按前缀获取配置
var dbConfig = dict.GetByPrefix("database", removePrefix: true);
// 结果: {"host": "localhost", "port": "5432", "name": "mydb"}

// 类型转换
string port = dict.GetValueOrDefault("database.port", "5432");
int portNumber = port.ConvertToOrDefault<int>(5432);
```

## 模板引擎

### 基本模板渲染

```csharp
using XiHan.Framework.Utils.Text.Template;

// 简单变量替换
string template = "你好，{{name}}！你今年{{age}}岁了。";
var values = new Dictionary<string, object?>
{
    { "name", "张三" },
    { "age", 30 }
};

string result = TemplateEngine.Render(template, values);
// 输出: "你好，张三！你今年30岁了。"

// 使用对象模型
var person = new { Name = "李四", Age = 25 };
string template2 = "姓名：{{Name}}，年龄：{{Age}}";
string result2 = TemplateEngine.Render(template2, person);
```

### 高级模板功能

```csharp
// 条件语句
string conditionalTemplate = @"
{{if IsVip}}
尊敬的 VIP 用户 {{Name}}，您好！
{{else}}
普通用户 {{Name}}，您好！
{{endif}}
";

var user = new Dictionary<string, object?>
{
    { "Name", "王五" },
    { "IsVip", true }
};

string result = TemplateEngine.RenderAdvanced(conditionalTemplate, user);

// 循环语句
string loopTemplate = @"
购物清单：
{{for item in Items}}
- {{item.Name}}: ￥{{item.Price}}
{{endfor}}
";

var shopping = new Dictionary<string, object?>
{
    { "Items", new[]
        {
            new { Name = "苹果", Price = 10.5 },
            new { Name = "香蕉", Price = 8.0 }
        }
    }
};

string shoppingList = TemplateEngine.RenderAdvanced(loopTemplate, shopping);
```

### 模板缓存

```csharp
using XiHan.Framework.Utils.Text.Template;

// 缓存模板以提高性能
TemplateCache.SetTemplate("greeting", "你好，{{name}}！");

// 从缓存获取并渲染
string cached = TemplateCache.GetTemplate("greeting");
string result = TemplateEngine.Render(cached, new { name = "赵六" });

// 清理缓存
TemplateCache.Clear();
```

### 文件模板

```csharp
// 从文件加载模板
string template = await FileTemplateHelper.LoadTemplateAsync("templates/email.txt");

// 渲染并保存到文件
var data = new { Title = "通知", Content = "这是邮件内容" };
await FileTemplateHelper.RenderToFileAsync("templates/email.txt", data, "output/email.txt");

// 批量处理模板
var templates = await FileTemplateHelper.LoadTemplatesFromDirectoryAsync("templates/");
```

### 模板扩展方法

```csharp
using XiHan.Framework.Utils.Text.Template;

// 字符串扩展
string template = "Hello, {{name}}!";
string result = template.RenderTemplate(new { name = "World" });

// 字典扩展
var values = new Dictionary<string, object?> { { "name", "张三" } };
string result2 = template.RenderTemplate(values);
```

## 字符串处理

### 字符串分割与组装

```csharp
using XiHan.Framework.Utils.Text;

// 分割字符串
string source = "苹果,香蕉,橙子,苹果";
List<string> items = StringHelper.GetStrList(source, ',', isAllowsDuplicates: false);
// 结果: ["苹果", "香蕉", "橙子"] (去重)

string[] array = StringHelper.GetStrArray(source);
IEnumerable<string> enumerable = StringHelper.GetStrEnumerable(source);

// 组装字符串
var fruits = new[] { "苹果", "香蕉", "橙子" };
string joined = StringHelper.GetListStr(fruits, ';');
// 结果: "苹果;香蕉;橙子"

// 从字典组装
var dict = new Dictionary<string, string>
{
    { "fruit1", "苹果" },
    { "fruit2", "香蕉" }
};
string dictStr = StringHelper.GetDictionaryValueStr(dict, '|');
```

### 字符串验证与转换

```csharp
// 数字ID验证
bool isNumberId = StringHelper.IsNumberId("12345"); // true
bool isNotNumberId = StringHelper.IsNumberId("abc"); // false

// 正则验证
bool isEmail = StringHelper.IsValidateStr(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", "test@example.com");

// 获取字符串长度（考虑中文字符）
int length = StringHelper.GetStrLength("你好World"); // 中文字符按2个字符计算

// 裁剪字符串
string clipped = StringHelper.ClipString("这是一个很长的字符串", 10);

// HTML转文本
string htmlText = "<p>这是<strong>HTML</strong>内容</p>";
string plainText = StringHelper.HtmlToTxt(htmlText); // "这是HTML内容"

// 首字母大小写转换
string upperFirst = StringHelper.FirstToUpper("hello"); // "Hello"
string lowerFirst = StringHelper.FirstToLower("Hello"); // "hello"
```

### 字符串扩展方法

```csharp
using XiHan.Framework.Utils.Text;

string text = "Hello World";

// 确保以特定字符开头/结尾
string withSlash = text.EnsureEndsWith('/');
string withPrefix = text.EnsureStartsWith('*');

// 空值检查
bool isEmpty = text.IsNullOrEmpty();
bool isWhiteSpace = "  ".IsNullOrWhiteSpace();

// 字符串截取
string left = text.Left(5); // "Hello"
string right = text.Right(5); // "World"

// 查找字符位置
int index = text.NthIndexOf('l', 2); // 第2个'l'的位置

// 前缀后缀处理
string removed = "filename.txt".RemovePostFix(".txt"); // "filename"
string withoutPrefix = "prefix_name".RemovePreFix("prefix_"); // "name"

// 替换操作
string replaced = text.ReplaceFirst("l", "L"); // "HeLlo World"

// 大小写转换
string camelCase = "hello_world".ToCamelCase(); // "helloWorld"
string pascalCase = "hello_world".ToPascalCase(); // "HelloWorld"
string kebabCase = "HelloWorld".ToKebabCase(); // "hello-world"
string snakeCase = "HelloWorld".ToSnakeCase(); // "hello_world"

// 字符串分割
string[] lines = text.SplitToLines();
string[] parts = text.Split(" ");

// 截断操作
string truncated = "很长的文本内容".Truncate(5); // "很长的文本..."
string truncatedPrefix = "很长的文本内容".TruncateWithPostfix(4, "…"); // "很长的文…"

// 格式化与填充
string formatted = "Hello {0}!".Format("World");
string padded = "test".PadToLength(10, '*', padLeft: true); // "******test"
string repeated = "abc".Repeat(3); // "abcabcabc"

// 编码操作
byte[] bytes = text.GetBytes();
byte[] utf8Bytes = text.GetBytes(Encoding.UTF8);

// 验证操作
bool isValidJson = "{\"name\":\"test\"}".IsValidJson();
bool containsChinese = "Hello世界".ContainsChinese();

// 字符串距离计算
int distance = "kitten".LevenshteinDistance("sitting"); // 编辑距离

// 包含检查
bool containsAny = text.ContainsAny(new[] { "Hello", "Hi" });
bool containsAll = text.ContainsAll(new[] { "Hello", "World" });

// 字符串反转
string reversed = "Hello".Reverse(); // "olleH"

// 安全子字符串
string safe = text.SafeSubstring(6, 5); // 不会抛出异常

// 移除字符
string noSpaces = "a b c".RemoveWhiteSpaces(); // "abc"
string noInvisible = "text\u200B\u200C".RemoveInvisibleChars(); // 移除不可见字符
```

## 编码转换

### Base64 编码

```csharp
using XiHan.Framework.Utils.Text;

string text = "Hello, 世界!";

// Base64 编码/解码
string base64 = text.Base64Encode();
string decoded = base64.Base64Decode();

// Base32 编码/解码
string base32 = text.Base32Encode();
string base32Decoded = base32.Base32Decode();
```

### URL 和 HTML 编码

```csharp
string url = "https://example.com/search?q=C# 编程";
string html = "<script>alert('Hello');</script>";

// URL 编码/解码
string urlEncoded = url.UrlEncode();
string urlDecoded = urlEncoded.UrlDecode();

// HTML 编码/解码
string htmlEncoded = html.HtmlEncode();
string htmlDecoded = htmlEncoded.HtmlDecode();
```

### Unicode 编码

```csharp
string chinese = "你好";

// Unicode 编码/解码
string unicode = chinese.UnicodeEncode(); // "\u4f60\u597d"
string unicodeDecoded = unicode.UnicodeDecode(); // "你好"
```

### 二进制转换

```csharp
string text = "Hello";

// 字符串转二进制
byte[] binary = text.BinaryEncode();
string binaryString = text.FromStringToBinary();

// 二进制转字符串
string recovered = binary.BinaryDecode();
string fromBinary = binaryString.FromBinaryToString();

// 转流
Stream stream = text.ToStream();
```

## 配置选项详解

### JSON 序列化选项

```csharp
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,                              // 格式化输出
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // 驼峰命名
    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,  // 字典键命名策略
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略null值
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,        // 中文字符不转义
    AllowTrailingCommas = true,                        // 允许尾随逗号
    ReadCommentHandling = JsonCommentHandling.Skip     // 跳过注释
};
```

### XML 序列化选项

```csharp
var xmlOptions = new XmlSerializationOptions
{
    OmitXmlDeclaration = false,    // 是否省略 XML 声明
    Indent = true,                 // 是否格式化输出
    IndentChars = "  ",           // 缩进字符
    Encoding = Encoding.UTF8,      // 编码格式
    OmitNamespaces = true,         // 是否省略命名空间
    CustomNamespaces = new Dictionary<string, string>
    {
        { "ns", "http://example.com/namespace" }
    }
};

// 预定义选项
var compact = XmlSerializationOptions.Compact;     // 紧凑格式
var formatted = XmlSerializationOptions.Formatted; // 格式化
var defaultOpts = XmlSerializationOptions.Default; // 默认
```

### YAML 序列化选项

```csharp
var yamlOptions = new YamlSerializeOptions
{
    IndentSize = 2,                    // 缩进大小
    IncludeDocumentMarkers = true,     // 包含文档标记 (--- 和 ...)
    HeaderComment = "配置文件",         // 头部注释
    ForceQuoteStrings = false,         // 强制字符串加引号
    SortKeys = true,                   // 按键排序
    UseFlowStyle = false               // 使用流式样式
};

// 预定义选项
var compact = YamlSerializeOptions.Compact;     // 紧凑格式
var formatted = YamlSerializeOptions.Formatted; // 格式化
var strict = YamlSerializeOptions.Strict;       // 严格格式
```

## 最佳实践

### 1. 错误处理

```csharp
try
{
    var person = json.FromJson<Person>();
}
catch (JsonException ex)
{
    // JSON 格式错误
    Console.WriteLine($"JSON 解析错误: {ex.Message}");
}
catch (ArgumentException ex)
{
    // 参数错误
    Console.WriteLine($"参数错误: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // 反序列化失败
    Console.WriteLine($"反序列化失败: {ex.Message}");
}
```

### 2. 性能优化

```csharp
// 对于大量数据，使用异步方法
var tasks = files.Select(async file =>
{
    var content = await File.ReadAllTextAsync(file);
    return await content.FromJsonAsync<ConfigModel>();
});

var results = await Task.WhenAll(tasks);

// 使用模板缓存提高模板渲染性能
TemplateCache.SetTemplate("email", emailTemplate);
for (int i = 0; i < 1000; i++)
{
    var result = TemplateCache.GetTemplate("email").RenderTemplate(data);
}
```

### 3. 配置管理

```csharp
// 使用 YAML 管理应用配置
public class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

// 加载配置
var config = await YamlHelper.DeserializeFromFileAsync<AppConfig>("appsettings.yaml");

// 保存配置
await config.ToYamlAsync(YamlSerializeOptions.Formatted)
    .ContinueWith(yaml => File.WriteAllTextAsync("appsettings.yaml", yaml.Result));

// 使用嵌套配置
var dbConfig = config.Database;
string connectionString = $"Host={dbConfig.Host};Port={dbConfig.Port};Database={dbConfig.Name}";
```

### 4. 数据验证

```csharp
// JSON 验证
if (!json.IsValidJson())
{
    throw new InvalidDataException("无效的 JSON 格式");
}

// XML 验证
if (!xml.IsValidXml(out string? error))
{
    throw new InvalidDataException($"无效的 XML: {error}");
}

// YAML 验证
if (!yaml.IsValidYaml())
{
    throw new InvalidDataException("无效的 YAML 格式");
}

// 字符串验证
if (!email.IsValidateStr(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$"))
{
    throw new ArgumentException("无效的邮箱格式");
}
```

### 5. 模板最佳实践

```csharp
// 使用强类型模板
public class EmailTemplate
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

// 分离模板和数据
var template = new EmailTemplate
{
    Subject = "欢迎 {{UserName}}",
    Body = @"
    亲爱的 {{UserName}}，

    {{if IsVip}}
    感谢您成为我们的 VIP 用户！
    {{else}}
    欢迎注册我们的服务！
    {{endif}}

    此致
    {{CompanyName}}
    "
};

var data = new
{
    UserName = "张三",
    IsVip = true,
    CompanyName = "XiHan科技"
};

string subject = TemplateEngine.RenderAdvanced(template.Subject, data);
string body = TemplateEngine.RenderAdvanced(template.Body, data);
```

### 6. 编码安全

```csharp
// 安全的 HTML 编码
string userInput = "<script>alert('xss')</script>";
string safeHtml = userInput.HtmlEncode(); // 防止 XSS 攻击

// 安全的 URL 编码
string urlParam = "特殊字符&参数";
string safeUrl = urlParam.UrlEncode();

// 二进制数据处理
byte[] sensitiveData = "敏感信息".BinaryEncode();
// 处理完后清除
Array.Clear(sensitiveData, 0, sensitiveData.Length);
```

## 注意事项

1. **编码问题**: 默认使用 UTF-8 编码，确保文件编码一致
2. **命名空间**: XML 序列化时注意命名空间的处理
3. **类型转换**: JSON/YAML 中的类型转换是基于字符串的，注意精度问题
4. **性能**: 对于大文件，建议使用异步方法和流式处理
5. **安全性**: 反序列化时要验证数据来源，避免安全风险
6. **内存管理**: 对于大量数据处理，注意及时释放资源
7. **模板复杂度**: 避免在模板中使用过于复杂的逻辑，保持简单明了
8. **缓存策略**: 合理使用模板缓存，避免内存泄漏

## 扩展功能

框架提供了丰富的扩展方法，支持：

- **链式调用**: 流畅的 API 设计
- **类型安全**: 强类型转换和验证
- **灵活配置**: 可定制的序列化选项
- **完善错误处理**: 详细的异常信息
- **异步支持**: 高性能的异步操作
- **模板引擎**: 强大的文本模板处理
- **编码转换**: 全面的编码/解码支持
- **字符串操作**: 丰富的字符串处理功能

通过这些功能，您可以轻松处理各种文本处理需求，从简单的字符串操作到复杂的数据序列化、模板渲染和编码转换。
