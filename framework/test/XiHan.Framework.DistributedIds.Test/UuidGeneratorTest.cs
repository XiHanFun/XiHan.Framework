using JetBrains.Annotations;
using XiHan.Framework.DistributedIds.Uuids;

namespace XiHan.Framework.DistributedIds.Test;

/// <summary>
/// UUID生成器测试
/// </summary>
[TestSubject(typeof(UuidGenerator))]
public class UuidGeneratorTest
{
    [Fact(DisplayName = "默认构造函数测试")]
    public void Constructor_Default_ShouldInitializeWithStandardType()
    {
        // Arrange & Act
        var generator = new UuidGenerator();

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal("UUID (Standard)", stats["GeneratorType"]);
        Assert.Equal(UuidTypes.Standard.ToString(), stats["UuidType"]);
    }

    [Fact(DisplayName = "指定UUID类型构造函数测试")]
    public void Constructor_WithUuidType_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var generator = new UuidGenerator(UuidTypes.RandomV4);

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal("UUID (RandomV4)", stats["GeneratorType"]);
        Assert.Equal(UuidTypes.RandomV4.ToString(), stats["UuidType"]);
    }

    [Fact(DisplayName = "基于名称空间名称的构造函数测试")]
    public void Constructor_WithNamespaceNameAndName_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var generator = new UuidGenerator(UuidTypes.NameBasedMD5, "DNS", "test.com");

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal("UUID (NameBasedMD5)", stats["GeneratorType"]);
        Assert.Equal(UuidTypes.NameBasedMD5.ToString(), stats["UuidType"]);
    }

    [Fact(DisplayName = "无效名称空间名称构造函数测试")]
    public void Constructor_WithInvalidNamespaceName_ShouldThrowException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new UuidGenerator(UuidTypes.NameBasedMD5, "InvalidNamespace", "test"));
    }

    [Fact(DisplayName = "基于GUID名称空间的构造函数测试")]
    public void Constructor_WithGuidNamespaceAndName_ShouldInitializeCorrectly()
    {
        // Arrange
        var namespaceGuid = Guid.NewGuid();

        // Act
        var generator = new UuidGenerator(UuidTypes.NameBasedSHA1, namespaceGuid, "test");

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal("UUID (NameBasedSHA1)", stats["GeneratorType"]);
        Assert.Equal(UuidTypes.NameBasedSHA1.ToString(), stats["UuidType"]);
    }

    [Fact(DisplayName = "标准UUID生成测试")]
    public void NextId_WithStandardType_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.Standard);

        // Act
        var id = generator.NextId();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "随机V4 UUID生成测试")]
    public void NextId_WithRandomV4_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.RandomV4);

        // Act
        var id = generator.NextId();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "基于时间的V1 UUID生成测试")]
    public void NextId_WithTimeBasedV1_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.TimeBasedV1);

        // Act
        var id = generator.NextId();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "基于MD5的UUID生成测试")]
    public void NextId_WithNameBasedMD5_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.NameBasedMD5, "DNS", "test.com");

        // Act
        var id = generator.NextId();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "基于SHA1的UUID生成测试")]
    public void NextId_WithNameBasedSHA1_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.NameBasedSHA1, "URL", "https://test.com");

        // Act
        var id = generator.NextId();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "顺序UUID生成测试")]
    public void NextId_WithSequential_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.Sequential);

        // Act
        var id = generator.NextId();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "生成字符串UUID测试")]
    public void NextIdString_ShouldGenerateValidString()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act
        var idString = generator.NextIdString();

        // Assert
        Assert.NotEmpty(idString);
        Assert.Equal(32, idString.Length); // UUID无连字符的长度
    }

    [Fact(DisplayName = "批量生成UUID测试")]
    public void NextIds_ShouldGenerateSpecifiedNumberOfIds()
    {
        // Arrange
        var generator = new UuidGenerator();
        var count = 5;

        // Act
        var ids = generator.NextIds(count);

        // Assert
        Assert.Equal(count, ids.Length);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact(DisplayName = "批量生成字符串UUID测试")]
    public void NextIdStrings_ShouldGenerateSpecifiedNumberOfStringIds()
    {
        // Arrange
        var generator = new UuidGenerator();
        var count = 5;

        // Act
        var idStrings = generator.NextIdStrings(count);

        // Assert
        Assert.Equal(count, idStrings.Length);
        Assert.Equal(count, idStrings.Distinct().Count());
    }

    [Fact(DisplayName = "异步生成UUID测试")]
    public async Task NextIdAsync_ShouldGenerateId()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act
        var id = await generator.NextIdAsync();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "异步生成字符串UUID测试")]
    public async Task NextIdStringAsync_ShouldGenerateStringId()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act
        var idString = await generator.NextIdStringAsync();

        // Assert
        Assert.NotEmpty(idString);
        Assert.Equal(32, idString.Length);
    }

    [Fact(DisplayName = "异步批量生成UUID测试")]
    public async Task NextIdsAsync_ShouldGenerateSpecifiedNumberOfIds()
    {
        // Arrange
        var generator = new UuidGenerator();
        var count = 5;

        // Act
        var ids = await generator.NextIdsAsync(count);

        // Assert
        Assert.Equal(count, ids.Length);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact(DisplayName = "异步批量生成字符串UUID测试")]
    public async Task NextIdStringsAsync_ShouldGenerateSpecifiedNumberOfStringIds()
    {
        // Arrange
        var generator = new UuidGenerator();
        var count = 5;

        // Act
        var idStrings = await generator.NextIdStringsAsync(count);

        // Assert
        Assert.Equal(count, idStrings.Length);
        Assert.Equal(count, idStrings.Distinct().Count());
    }

    [Fact(DisplayName = "顺序UUID时间提取测试")]
    public void ExtractTime_WithSequentialType_ShouldReturnTimeCloseToNow()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.Sequential);
        var beforeGeneration = DateTime.UtcNow;
        var id = generator.NextId();
        var afterGeneration = DateTime.UtcNow;

        // Act
        var extractedTime = generator.ExtractTime(id);

        // Assert
        Assert.True(extractedTime >= beforeGeneration.AddSeconds(-1));
        Assert.True(extractedTime <= afterGeneration.AddSeconds(1));
    }

    [Fact(DisplayName = "时间V1 UUID时间提取测试")]
    public void ExtractTime_WithTimeBasedV1Type_ShouldReturnTime()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.TimeBasedV1);
        var id = generator.NextId();

        // Act
        var extractedTime = generator.ExtractTime(id);

        // Assert
        // 应该返回一个有效的日期时间（大于UUID v1基准时间1582年10月15日）
        Assert.True(extractedTime > new DateTime(1582, 10, 15));
        Assert.True(extractedTime <= DateTime.UtcNow);
    }

    [Fact(DisplayName = "非时间类型UUID时间提取测试")]
    public void ExtractTime_WithNonTimeBasedType_ShouldReturnCurrentTime()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.RandomV4);
        var beforeGeneration = DateTime.UtcNow;
        var id = generator.NextId();
        var afterGeneration = DateTime.UtcNow.AddSeconds(1);

        // Act
        var extractedTime = generator.ExtractTime(id);

        // Assert
        // 非时间类型UUID不包含时间信息，应返回当前时间
        Assert.True(extractedTime >= beforeGeneration);
        Assert.True(extractedTime <= afterGeneration);
    }

    [Fact(DisplayName = "工作机器ID提取测试")]
    public void ExtractWorkerId_ShouldReturnZero()
    {
        // Arrange
        var generator = new UuidGenerator();
        var id = generator.NextId();

        // Act
        var workerId = generator.ExtractWorkerId(id);

        // Assert
        Assert.Equal(0, workerId); // UUID不包含工作机器ID信息
    }

    [Fact(DisplayName = "序列号提取测试")]
    public void ExtractSequence_ShouldReturnZero()
    {
        // Arrange
        var generator = new UuidGenerator();
        var id = generator.NextId();

        // Act
        var sequence = generator.ExtractSequence(id);

        // Assert
        Assert.Equal(0, sequence); // UUID不包含序列号信息
    }

    [Fact(DisplayName = "数据中心ID提取测试")]
    public void ExtractDataCenterId_ShouldReturnZero()
    {
        // Arrange
        var generator = new UuidGenerator();
        var id = generator.NextId();

        // Act
        var dataCenterId = generator.ExtractDataCenterId(id);

        // Assert
        Assert.Equal(0, dataCenterId); // UUID不包含数据中心ID信息
    }

    [Fact(DisplayName = "获取生成器类型测试")]
    public void GetGeneratorType_ShouldReturnCorrectType()
    {
        // Arrange
        var standardGenerator = new UuidGenerator(UuidTypes.Standard);
        var randomGenerator = new UuidGenerator(UuidTypes.RandomV4);
        var timeGenerator = new UuidGenerator(UuidTypes.TimeBasedV1);
        var md5Generator = new UuidGenerator(UuidTypes.NameBasedMD5, "DNS", "test");
        var sha1Generator = new UuidGenerator(UuidTypes.NameBasedSHA1, "URL", "test");
        var seqGenerator = new UuidGenerator(UuidTypes.Sequential);

        // Act & Assert
        Assert.Equal("UUID (Standard)", standardGenerator.GetGeneratorType());
        Assert.Equal("UUID (RandomV4)", randomGenerator.GetGeneratorType());
        Assert.Equal("UUID (TimeBasedV1)", timeGenerator.GetGeneratorType());
        Assert.Equal("UUID (NameBasedMD5)", md5Generator.GetGeneratorType());
        Assert.Equal("UUID (NameBasedSHA1)", sha1Generator.GetGeneratorType());
        Assert.Equal("UUID (Sequential)", seqGenerator.GetGeneratorType());
    }

    [Fact(DisplayName = "UUID基于名称生成器状态信息测试")]
    public void GetStats_WithNameBasedGenerator_ShouldIncludeNamespaceInfo()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.NameBasedMD5, "DNS", "test.com");

        // Act
        var stats = generator.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.ContainsKey("GeneratorType"));
        Assert.True(stats.ContainsKey("UuidType"));
        Assert.True(stats.ContainsKey("Namespace"));
        Assert.True(stats.ContainsKey("Name"));
    }

    [Fact(DisplayName = "UUID其他类型生成器状态信息测试")]
    public void GetStats_WithOtherTypes_ShouldNotIncludeNamespaceInfo()
    {
        // Arrange
        var generator = new UuidGenerator(UuidTypes.RandomV4);

        // Act
        var stats = generator.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.ContainsKey("GeneratorType"));
        Assert.True(stats.ContainsKey("UuidType"));
        Assert.False(stats.ContainsKey("Namespace"));
        Assert.False(stats.ContainsKey("Name"));
    }

    [Fact(DisplayName = "批量生成UUID参数验证测试")]
    public void NextIds_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => generator.NextIds(0));
        Assert.Throws<ArgumentException>(() => generator.NextIds(-1));
    }

    [Fact(DisplayName = "批量生成字符串UUID参数验证测试")]
    public void NextIdStrings_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => generator.NextIdStrings(0));
        Assert.Throws<ArgumentException>(() => generator.NextIdStrings(-1));
    }

    [Fact(DisplayName = "异步批量生成UUID参数验证测试")]
    public async Task NextIdsAsync_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdsAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdsAsync(-1));
    }

    [Fact(DisplayName = "异步批量生成字符串UUID参数验证测试")]
    public async Task NextIdStringsAsync_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var generator = new UuidGenerator();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdStringsAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdStringsAsync(-1));
    }
}
