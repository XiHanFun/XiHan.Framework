using JetBrains.Annotations;
using System.Text.RegularExpressions;
using XiHan.Framework.DistributedIds.NanoIds;

namespace XiHan.Framework.DistributedIds.Test;

/// <summary>
/// NanoID生成器测试
/// </summary>
[TestSubject(typeof(NanoIdGenerator))]
public class NanoIdGeneratorTest
{
    [Fact(DisplayName = "默认配置创建NanoID生成器测试")]
    public void Constructor_WithDefaultOptions_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var generator = new NanoIdGenerator();

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal("NanoID", stats["GeneratorType"]);
        Assert.Equal(21, stats["Size"]);
        Assert.Equal(NanoIdOptions.DefaultAlphabet, stats["Alphabet"]);
    }

    [Fact(DisplayName = "自定义配置创建NanoID生成器测试")]
    public void Constructor_WithCustomOptions_ShouldInitializeCorrectly()
    {
        // Arrange
        var options = new NanoIdOptions
        {
            Size = 16,
            Alphabet = "abc123"
        };

        // Act
        var generator = new NanoIdGenerator(options);

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal(16, stats["Size"]);
        Assert.Equal("abc123", stats["Alphabet"]);
    }

    [Fact(DisplayName = "生成唯一ID测试")]
    public void NextId_ShouldGenerateUniqueIds()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var count = 1000;
        var ids = new HashSet<long>();

        // Act
        for (var i = 0; i < count; i++)
        {
            ids.Add(generator.NextId());
        }

        // Assert
        Assert.Equal(count, ids.Count);
    }

    [Fact(DisplayName = "生成字符串ID测试")]
    public void NextIdString_ShouldGenerateValidStringId()
    {
        // Arrange
        var generator = new NanoIdGenerator();

        // Act
        var idString = generator.NextIdString();

        // Assert
        Assert.NotEmpty(idString);
        Assert.Equal(21, idString.Length); // 默认长度为21

        // 验证只包含有效字符
        var validChars = new HashSet<char>(NanoIdOptions.DefaultAlphabet);
        foreach (var c in idString)
        {
            Assert.Contains(c, validChars);
        }
    }

    [Fact(DisplayName = "生成自定义长度字符串ID测试")]
    public void NextIdString_WithCustomSize_ShouldHaveCorrectLength()
    {
        // Arrange
        var size = 10;
        var options = new NanoIdOptions { Size = size };
        var generator = new NanoIdGenerator(options);

        // Act
        var idString = generator.NextIdString();

        // Assert
        Assert.Equal(size, idString.Length);
    }

    [Fact(DisplayName = "生成自定义字符集ID测试")]
    public void NextIdString_WithCustomAlphabet_ShouldUseCorrectChars()
    {
        // Arrange
        var alphabet = "0123456789";
        var options = new NanoIdOptions { Alphabet = alphabet };
        var generator = new NanoIdGenerator(options);

        // Act
        var idString = generator.NextIdString();

        // Assert
        var regex = new Regex($"^[{alphabet}]+$");
        Assert.Matches(regex, idString);
    }

    [Fact(DisplayName = "批量生成ID测试")]
    public void NextIds_ShouldGenerateSpecifiedNumberOfIds()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var count = 5;

        // Act
        var ids = generator.NextIds(count);

        // Assert
        Assert.Equal(count, ids.Length);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact(DisplayName = "批量生成字符串ID测试")]
    public void NextIdStrings_ShouldGenerateSpecifiedNumberOfStringIds()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var count = 5;

        // Act
        var idStrings = generator.NextIdStrings(count);

        // Assert
        Assert.Equal(count, idStrings.Length);
        Assert.Equal(count, idStrings.Distinct().Count());
    }

    [Fact(DisplayName = "异步生成ID测试")]
    public async Task NextIdAsync_ShouldGenerateId()
    {
        // Arrange
        var generator = new NanoIdGenerator();

        // Act
        var id = await generator.NextIdAsync();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "异步生成字符串ID测试")]
    public async Task NextIdStringAsync_ShouldGenerateStringId()
    {
        // Arrange
        var generator = new NanoIdGenerator();

        // Act
        var idString = await generator.NextIdStringAsync();

        // Assert
        Assert.NotEmpty(idString);
        Assert.Equal(21, idString.Length);
    }

    [Fact(DisplayName = "异步批量生成ID测试")]
    public async Task NextIdsAsync_ShouldGenerateSpecifiedNumberOfIds()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var count = 5;

        // Act
        var ids = await generator.NextIdsAsync(count);

        // Assert
        Assert.Equal(count, ids.Length);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact(DisplayName = "异步批量生成字符串ID测试")]
    public async Task NextIdStringsAsync_ShouldGenerateSpecifiedNumberOfStringIds()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var count = 5;

        // Act
        var idStrings = await generator.NextIdStringsAsync(count);

        // Assert
        Assert.Equal(count, idStrings.Length);
        Assert.Equal(count, idStrings.Distinct().Count());
    }

    [Fact(DisplayName = "从ID提取时间测试")]
    public void ExtractTime_ShouldReturnCorrectTime()
    {
        // Arrange
        var options = new NanoIdOptions();
        var generator = new NanoIdGenerator(options);
        var beforeGeneration = DateTime.UtcNow;
        var id = generator.NextId();
        var afterGeneration = DateTime.UtcNow;

        // Act
        var extractedTime = generator.ExtractTime(id);

        // Assert
        Assert.True(extractedTime >= options.StartTime);
        Assert.True(extractedTime >= beforeGeneration.AddSeconds(-1));
        Assert.True(extractedTime <= afterGeneration.AddSeconds(1));
    }

    [Fact(DisplayName = "从ID提取序列号测试")]
    public void ExtractSequence_ShouldReturnValidSequence()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var id = generator.NextId();

        // Act
        var sequence = generator.ExtractSequence(id);

        // Assert
        Assert.True(sequence >= 0);
        Assert.True(sequence <= 0x3FFFFF); // 22位
    }

    [Fact(DisplayName = "NanoID不同配置测试")]
    public void NanoIdOptions_DifferentConfigurations_ShouldWork()
    {
        // 只有数字配置
        var numericGenerator = new NanoIdGenerator(NanoIdOptions.OnlyNumbers(8));
        var numericId = numericGenerator.NextIdString();
        Assert.Equal(8, numericId.Length);
        Assert.Matches("^[0-9]+$", numericId);

        // 只有小写字母配置
        var lowercaseGenerator = new NanoIdGenerator(NanoIdOptions.OnlyLowercase(12));
        var lowercaseId = lowercaseGenerator.NextIdString();
        Assert.Equal(12, lowercaseId.Length);
        Assert.Matches("^[a-z]+$", lowercaseId);

        // 只有大写字母配置
        var uppercaseGenerator = new NanoIdGenerator(NanoIdOptions.OnlyUppercase(14));
        var uppercaseId = uppercaseGenerator.NextIdString();
        Assert.Equal(14, uppercaseId.Length);
        Assert.Matches("^[A-Z]+$", uppercaseId);

        // URL安全配置
        var urlSafeGenerator = new NanoIdGenerator(NanoIdOptions.UrlSafe(16));
        var urlSafeId = urlSafeGenerator.NextIdString();
        Assert.Equal(16, urlSafeId.Length);
        Assert.Matches("^[a-zA-Z0-9_-]+$", urlSafeId);

        // 十六进制配置
        var hexGenerator = new NanoIdGenerator(NanoIdOptions.Hex(24));
        var hexId = hexGenerator.NextIdString();
        Assert.Equal(24, hexId.Length);
        Assert.Matches("^[0-9a-f]+$", hexId);
    }

    [Fact(DisplayName = "通过工厂创建NanoID生成器测试")]
    public void IdGeneratorFactory_CreateNanoIdGenerator_ShouldWork()
    {
        // 默认NanoID生成器
        var defaultGenerator = IdGeneratorFactory.CreateNanoIdGenerator();
        Assert.Equal("NanoID", defaultGenerator.GetGeneratorType());

        // 数字NanoID生成器
        var numericGenerator = IdGeneratorFactory.CreateNanoIdGenerator_Numeric(12);
        var numericId = numericGenerator.NextIdString();
        Assert.Equal(12, numericId.Length);
        Assert.Matches("^[0-9]+$", numericId);

        // URL安全NanoID生成器
        var urlSafeGenerator = IdGeneratorFactory.CreateNanoIdGenerator_UrlSafe(18);
        var urlSafeId = urlSafeGenerator.NextIdString();
        Assert.Equal(18, urlSafeId.Length);
        Assert.Matches("^[a-zA-Z0-9_-]+$", urlSafeId);

        // 自定义字符集NanoID生成器
        var customGenerator = IdGeneratorFactory.CreateNanoIdGenerator_Custom("ACGT", 30); // DNA序列风格
        var customId = customGenerator.NextIdString();
        Assert.Equal(30, customId.Length);
        Assert.Matches("^[ACGT]+$", customId);
    }

    [Fact(DisplayName = "NanoID生成器性能测试")]
    public void NanoIdGenerator_Performance_ShouldBeEfficient()
    {
        // Arrange
        var generator = new NanoIdGenerator();
        var count = 10000;
        var stopwatch = new System.Diagnostics.Stopwatch();

        // Act
        stopwatch.Start();
        var ids = new HashSet<string>(count);

        for (var i = 0; i < count; i++)
        {
            ids.Add(generator.NextIdString());
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.Equal(count, ids.Count); // 确保所有ID都是唯一的

        // 输出性能信息(每秒生成的ID数)
        var idsPerSecond = count / (elapsedMs / 1000.0);
        Console.WriteLine($"生成 {count:N0} 个NanoID耗时: {elapsedMs}毫秒");
        Console.WriteLine($"生成速率: {idsPerSecond:N0} IDs/秒");

        // 合理的性能期望(每秒应该能生成至少10000个ID)
        Assert.True(idsPerSecond > 10000, $"性能太低: {idsPerSecond:N0} IDs/秒");
    }

    [Fact(DisplayName = "5位短ID高性能生成测试")]
    public void NanoIdGenerator_ShortIdPerformance_ShouldGenerateQuickly()
    {
        var options = new NanoIdOptions
        {
            Size = 5
        };
        var generator = new NanoIdGenerator(options);
        var count = 10000;
        var stopwatch = new System.Diagnostics.Stopwatch();

        // Act - 生成10000个ID并测量性能
        stopwatch.Start();
        var ids = new HashSet<string>(count);

        for (var i = 0; i < count; i++)
        {
            ids.Add(generator.NextIdString());
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Assert
        // 1. 检查是否有10000个唯一ID
        Assert.Equal(count, ids.Count);

        // 2. 检查ID长度和格式
        foreach (var id in ids)
        {
            Assert.Equal(5, id.Length);
            Assert.Matches("^[a-zA-Z0-9]+$", id);
        }

        // 3. 检查性能 - 确保能在1秒内生成10000个ID
        Console.WriteLine($"生成 {count:N0} 个5位短ID耗时: {elapsedMs}毫秒");
        var idsPerSecond = count / (elapsedMs / 1000.0);
        Console.WriteLine($"生成速率: {idsPerSecond:N0} IDs/秒");

        // 验证在1秒内能生成至少10000个ID
        Assert.True(elapsedMs <= 1000, $"生成10000个ID用时超过1秒: {elapsedMs}毫秒");
        Assert.True(idsPerSecond >= 10000, $"每秒生成ID数量不足10000: {idsPerSecond:N0} IDs/秒");
    }

    [Fact(DisplayName = "短ID生成与时间反推测试")]
    public void NanoIdGenerator_ShortIdAndTimeExtraction_ShouldWork()
    {
        // Arrange - 创建5位ID生成器
        var options = new NanoIdOptions
        {
            Size = 5,
            // 记录当前时间作为起始时间，用于后续验证
            StartTime = DateTime.UtcNow.AddSeconds(-10) // 提前10秒，便于观察
        };
        var generator = new NanoIdGenerator(options);
        var count = 200;
        var stopwatch = new System.Diagnostics.Stopwatch();

        // Act - 生成200个ID并测量性能
        stopwatch.Start();

        // 记录生成前的时间
        var beforeGeneration = DateTime.UtcNow;

        // 生成数值型ID和字符串ID
        var numericIds = new HashSet<long>(count);
        var stringIds = new HashSet<string>(count);
        var idTimePairs = new Dictionary<long, DateTime>(count);

        for (var i = 0; i < count; i++)
        {
            var numericId = generator.NextId();
            var stringId = generator.NextIdString();

            numericIds.Add(numericId);
            stringIds.Add(stringId);

            // 记录每个ID生成的时间
            idTimePairs[numericId] = DateTime.UtcNow;
        }

        // 记录生成后的时间
        var afterGeneration = DateTime.UtcNow;

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Assert
        // 1. 验证性能 - 1秒内能生成200个ID
        Console.WriteLine($"生成 {count} 个5位ID耗时: {elapsedMs}毫秒");
        Assert.True(elapsedMs <= 1000, $"生成{count}个ID用时超过1秒: {elapsedMs}毫秒");

        // 2. 验证唯一性
        Assert.Equal(count, numericIds.Count);
        Assert.Equal(count, stringIds.Count);

        // 3. 验证ID长度和格式
        foreach (var id in stringIds)
        {
            Assert.Equal(5, id.Length);
        }

        // 4. 验证时间反推功能
        var timeExtractionSuccessCount = 0;
        var maxTimeDifference = TimeSpan.Zero;

        foreach (var pair in idTimePairs)
        {
            var extractedTime = generator.ExtractTime(pair.Key);
            var actualTime = pair.Value;

            // 计算提取时间和实际时间的差异
            var difference = (actualTime - extractedTime).Duration();
            if (difference > maxTimeDifference)
            {
                maxTimeDifference = difference;
            }

            // 如果时间差异在1秒以内，视为成功
            if (difference <= TimeSpan.FromSeconds(1))
            {
                timeExtractionSuccessCount++;
            }
        }

        // 输出时间提取成功率和最大误差
        var successRate = (double)timeExtractionSuccessCount / count * 100;
        Console.WriteLine($"时间提取成功率: {successRate:F2}%");
        Console.WriteLine($"最大时间差异: {maxTimeDifference.TotalMilliseconds:F0}毫秒");

        // 验证时间提取成功率至少为90%
        Assert.True(successRate >= 90, $"时间提取成功率过低: {successRate:F2}%");

        // 5. 估算碰撞概率和使用寿命
        // 对于5位ID，使用默认字符集(A-Za-z0-9_-)，可能的组合数为64^5 = 1,073,741,824
        var possibleCombinations = Math.Pow(NanoIdOptions.DefaultAlphabet.Length, 5);

        // 计算碰撞概率 (使用生日悖论公式的近似值)
        // 当生成的ID数量达到 sqrt(2 * possibleCombinations * ln(1/(1-p))) 时，
        // 碰撞概率达到p。这里我们计算50%碰撞概率时的ID数量
        var idsForFiftyPercentCollision = Math.Sqrt(2 * possibleCombinations * Math.Log(1 / 0.5));

        // 根据当前生成速率，计算达到50%碰撞概率所需的时间
        var generationRate = count / (elapsedMs / 1000.0); // 每秒生成ID数
        var yearsUntilCollision = idsForFiftyPercentCollision / generationRate / 86400 / 365;

        Console.WriteLine($"5位ID可能的组合数: {possibleCombinations:N0}");
        Console.WriteLine($"50%碰撞概率所需ID数量: {idsForFiftyPercentCollision:N0}");
        Console.WriteLine($"按每秒{generationRate:N0}个ID的速率，大约{yearsUntilCollision:N2}年后达到50%碰撞概率");

        // 假设可接受碰撞概率为1%
        var idsForOnePercentCollision = Math.Sqrt(2 * possibleCombinations * Math.Log(1 / 0.99));
        var yearsUntilOnePercentCollision = idsForOnePercentCollision / generationRate / 86400 / 365;

        Console.WriteLine($"1%碰撞概率所需ID数量: {idsForOnePercentCollision:N0}");
        Console.WriteLine($"按每秒{generationRate:N0}个ID的速率，大约{yearsUntilOnePercentCollision:N2}年后达到1%碰撞概率");
    }
}
