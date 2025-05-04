# Unicode 文本数字水印工具

`TextWatermarkHelper` 是一个基于 Unicode 不可见字符的文本数字水印工具，可以在文本内容中嵌入不可见的版权标识和元数据信息，而不改变文本的可见外观和排版。

## 主要功能

- **版权保护**：在文章中嵌入不可见水印，有效保护原创内容
- **元数据嵌入**：在文本中安全地嵌入结构化信息，用于内容验证
- **来源追踪**：通过嵌入的水印信息，可以验证内容的原始来源
- **跨平台支持**：可在任何支持 Unicode 的平台上使用
- **HTML 集成**：提供针对 HTML 内容的专用水印方法
- **批量水印**：支持为多个文本块批量添加水印
- **加密支持**：可选择对水印信息进行加密保护

## 使用方法

### 基本使用

```csharp
// 原始文本
var text = "这是一段需要保护的原创内容...";

// 嵌入水印
var watermark = "版权所有©2024 ZhaiFanhua";
var watermarkedText = TextWatermarkHelper.EmbedWatermark(text, watermark);

// 检查是否包含水印
bool hasWatermark = TextWatermarkHelper.ContainsWatermark(watermarkedText);

// 提取水印
string extractedWatermark = TextWatermarkHelper.ExtractWatermark(watermarkedText);
```

### 加密水印

```csharp
// 使用密钥加密水印
var key = "YourSecretKey";
var watermarkedText = TextWatermarkHelper.EmbedWatermark(text, watermark, key);

// 使用相同的密钥提取水印
var extractedWatermark = TextWatermarkHelper.ExtractWatermark(watermarkedText, key);
```

### 元数据水印

```csharp
// 创建元数据对象
var metadata = new ArticleMetadata
{
    Author = "张三",
    PublishDate = DateTime.Now,
    ContentId = Guid.NewGuid().ToString()
};

// 嵌入元数据水印
var watermarkedText = TextWatermarkHelper.EmbedMetadata(text, metadata);

// 提取元数据水印
var extractedMetadata = TextWatermarkHelper.ExtractMetadata<ArticleMetadata>(watermarkedText);
```

### HTML 水印

```csharp
// HTML内容
var htmlContent = "<div><h1>标题</h1><p>文本内容</p></div>";

// 嵌入HTML水印
var watermarkedHtml = TextWatermarkHelper.EmbedHtmlWatermark(htmlContent, watermark);

// 提取HTML水印
var extractedWatermark = TextWatermarkHelper.ExtractHtmlWatermark(watermarkedHtml);
```

### 批量水印

```csharp
// 多个文本块
var textBlocks = new List<string> { "文本1", "文本2", "文本3" };

// 批量嵌入水印（每个文本块会有唯一编号）
var identifier = "ArticleSet_20240629";
var watermarkedBlocks = TextWatermarkHelper.EmbedBatchWatermark(textBlocks, identifier);
```

## 技术原理

1. **Unicode 不可见字符**：利用 Unicode 中的零宽字符（如零宽空格、零宽连字等）作为水印载体
2. **水印编码**：将水印信息转换为特定编码，再映射到不可见字符
3. **文本分析**：在文本的自然分隔处（如句子结尾）插入水印，确保不影响排版和可读性
4. **加密保护**：可选择使用 AES 加密算法对水印信息进行加密

## 应用场景

- **内容创作者**：为原创内容添加数字水印，保护知识产权
- **版权管理**：为数字内容添加不可见的版权标识
- **内容验证**：在公开文本中嵌入验证信息，确保内容完整性
- **信息溯源**：识别和追踪数字内容的传播路径

## 注意事项

1. 水印提取需要完整的文本，如果文本被严重修改，可能会导致水印丢失或无法完整提取
2. 水印使用的 Unicode 不可见字符在某些编辑器中可能会被显示为特殊符号或被过滤
3. 对于需要高安全性的场景，建议使用加密选项保护水印信息
4. 过多的水印可能会略微增加文本大小，但通常不会影响正常使用

## 示例代码

完整的使用示例请参考 `TextWatermarkExample.cs` 文件。
