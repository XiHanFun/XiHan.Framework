using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Test.Security;

/// <summary>
/// 文本水印使用示例
/// </summary>
public static class TextWatermarkExample
{
    /// <summary>
    /// 基本水印示例
    /// </summary>
    public static void BasicWatermarkExample()
    {
        // 原始文本内容
        var originalText = "这是一段需要保护的原创内容，通过Unicode水印技术可以在不改变可见文本的情况下，嵌入不可见的版权信息。";

        // 水印信息
        var watermark = "版权所有©2024 ZhaiFanhua";

        // 嵌入水印
        var watermarkedText = TextWatermarkHelper.EmbedWatermark(originalText, watermark);

        // 检查是否包含水印
        var hasWatermark = TextWatermarkHelper.ContainsWatermark(watermarkedText);

        // 提取水印
        var extractedWatermark = TextWatermarkHelper.ExtractWatermark(watermarkedText);

        // 输出结果
        Console.WriteLine($"原始文本: {originalText}");
        Console.WriteLine($"水印信息: {watermark}");
        Console.WriteLine($"包含水印的文本: {watermarkedText}");
        Console.WriteLine($"是否包含水印: {hasWatermark}");
        Console.WriteLine($"提取的水印: {extractedWatermark}");
    }

    /// <summary>
    /// 带加密的水印示例
    /// </summary>
    public static void EncryptedWatermarkExample()
    {
        // 原始文本内容
        var originalText = "这是一段包含机密信息的内容，需要使用加密水印保护。";

        // 水印信息
        var watermark = "机密级别：高度机密，仅限内部使用";

        // 加密密钥
        var key = "XiHanFramework@2024";

        // 嵌入加密水印
        var watermarkedText = TextWatermarkHelper.EmbedWatermark(originalText, watermark, key);

        // 提取水印（使用正确的密钥）
        var extractedWatermark = TextWatermarkHelper.ExtractWatermark(watermarkedText, key);

        // 尝试使用错误的密钥提取水印
        var wrongKey = "WrongKey@2024";
        var failedExtract = TextWatermarkHelper.ExtractWatermark(watermarkedText, wrongKey);

        // 输出结果
        Console.WriteLine($"原始文本: {originalText}");
        Console.WriteLine($"水印信息: {watermark}");
        Console.WriteLine($"包含加密水印的文本: {watermarkedText}");
        Console.WriteLine($"使用正确密钥提取的水印: {extractedWatermark}");
        Console.WriteLine($"使用错误密钥提取的水印: {failedExtract}");
    }

    /// <summary>
    /// 元数据水印示例
    /// </summary>
    public static void MetadataWatermarkExample()
    {
        // 原始文本内容
        var originalText = "这是一篇博客文章，我们将在其中嵌入作者、发布日期等元数据信息。";

        // 创建元数据对象
        var metadata = new ArticleMetadata
        {
            Author = "张三",
            PublishDate = DateTime.Now,
            ContentId = Guid.NewGuid().ToString(),
            Tags = ["水印", "安全", "版权保护"]
        };

        // 嵌入元数据水印
        var watermarkedText = TextWatermarkHelper.EmbedMetadata(originalText, metadata);

        // 提取元数据水印
        var extractedMetadata = TextWatermarkHelper.ExtractMetadata<ArticleMetadata>(watermarkedText);

        // 输出结果
        Console.WriteLine($"原始文本: {originalText}");
        Console.WriteLine($"元数据: 作者={metadata.Author}, 发布日期={metadata.PublishDate}, ID={metadata.ContentId}");
        Console.WriteLine($"包含元数据水印的文本: {watermarkedText}");

        if (extractedMetadata != null)
        {
            Console.WriteLine($"提取的元数据: 作者={extractedMetadata.Author}, 发布日期={extractedMetadata.PublishDate}, ID={extractedMetadata.ContentId}");
            Console.WriteLine($"标签: {string.Join(", ", extractedMetadata.Tags)}");
        }
    }

    /// <summary>
    /// HTML水印示例
    /// </summary>
    public static void HtmlWatermarkExample()
    {
        // 原始HTML内容
        var htmlContent = "<div><h1>Unicode文本水印技术</h1><p>这是一个HTML文档示例，我们将在其中嵌入不可见的水印。</p></div>";

        // 水印信息
        var watermark = "HTML文档版权信息@2024";

        // 嵌入HTML水印
        var watermarkedHtml = TextWatermarkHelper.EmbedHtmlWatermark(htmlContent, watermark);

        // 提取HTML水印
        var extractedWatermark = TextWatermarkHelper.ExtractHtmlWatermark(watermarkedHtml);

        // 输出结果
        Console.WriteLine($"原始HTML: {htmlContent}");
        Console.WriteLine($"水印信息: {watermark}");
        Console.WriteLine($"包含水印的HTML: {watermarkedHtml}");
        Console.WriteLine($"提取的水印: {extractedWatermark}");
    }

    /// <summary>
    /// 批量水印示例
    /// </summary>
    public static void BatchWatermarkExample()
    {
        // 多个文本块
        var textBlocks = new List<string>
        {
            "这是第一段文本内容，需要添加水印。",
            "这是第二段文本内容，同样需要添加水印。",
            "这是第三段文本内容，也需要添加水印。"
        };

        // 统一标识符
        var identifier = "XiHanFramework_Article_20240629";

        // 批量嵌入水印
        var watermarkedBlocks = TextWatermarkHelper.EmbedBatchWatermark(textBlocks, identifier).ToList();

        // 输出结果
        Console.WriteLine("批量水印示例：");

        for (var i = 0; i < watermarkedBlocks.Count; i++)
        {
            Console.WriteLine($"原始文本块 {i + 1}: {textBlocks[i]}");
            Console.WriteLine($"包含水印的文本块 {i + 1}: {watermarkedBlocks[i]}");

            // 提取水印
            var extractedWatermark = TextWatermarkHelper.ExtractWatermark(watermarkedBlocks[i]);
            Console.WriteLine($"提取的水印 {i + 1}: {extractedWatermark}");
            Console.WriteLine();
        }
    }
}

/// <summary>
/// 文章元数据示例类
/// </summary>
public class ArticleMetadata
{
    /// <summary>
    /// 作者
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 发布日期
    /// </summary>
    public DateTime PublishDate { get; set; }

    /// <summary>
    /// 内容ID
    /// </summary>
    public string? ContentId { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public string[]? Tags { get; set; }
}
