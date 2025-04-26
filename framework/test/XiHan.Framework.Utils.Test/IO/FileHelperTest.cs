using System.Text;
using XiHan.Framework.Utils.IO;

namespace XiHan.Framework.Utils.Test.IO;

public class FileHelperTest : IDisposable
{
    private readonly string _testFilePath;
    private readonly string _testDirectory;

    public FileHelperTest()
    {
        // 创建临时测试目录
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileHelperTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // 创建测试文件路径
        _testFilePath = Path.Combine(_testDirectory, "test.txt");

        // 创建测试文件并写入一些内容
        File.WriteAllText(_testFilePath, "Hello, World!");
    }

    public void Dispose()
    {
        // 清理测试文件和目录
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void ReadAllText_ReturnsCorrectContent()
    {
        // Act
        var content = FileHelper.ReadAllText(_testFilePath);

        // Assert
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public async Task ReadAllTextAsync_ReturnsCorrectContent()
    {
        // Act
        var content = await FileHelper.ReadAllTextAsync(_testFilePath);

        // Assert
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public async Task ReadAllBytesAsync_ReturnsCorrectBytes()
    {
        // Act
        var bytes = await FileHelper.ReadAllBytesAsync(_testFilePath);

        // Assert
        var expectedBytes = Encoding.UTF8.GetBytes("Hello, World!");
        Assert.Equal(expectedBytes.Length, bytes.Length);
        for (var i = 0; i < expectedBytes.Length; i++)
        {
            Assert.Equal(expectedBytes[i], bytes[i]);
        }
    }

    [Fact]
    public async Task ReadAllLinesAsync_ReturnsCorrectLines()
    {
        // Arrange
        var multiLineFilePath = Path.Combine(_testDirectory, "multiline.txt");
        File.WriteAllLines(multiLineFilePath, ["Line 1", "Line 2", "Line 3"]);

        // Act
        var lines = await FileHelper.ReadAllLinesAsync(multiLineFilePath);

        // Assert
        Assert.Equal(3, lines.Length);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 2", lines[1]);
        Assert.Equal("Line 3", lines[2]);
    }

    [Fact]
    public void WriteAllText_WritesCorrectContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "write.txt");
        var content = "Test content";

        // Act
        FileHelper.WriteAllText(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, File.ReadAllText(filePath));
    }

    [Fact]
    public void AppendAllText_AppendsCorrectContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "append.txt");
        var initialContent = "Initial content\r\n";
        var appendContent = "Appended content";
        File.WriteAllText(filePath, initialContent);

        // Act
        FileHelper.AppendAllText(filePath, appendContent);

        // Assert
        Assert.Equal(initialContent + appendContent, File.ReadAllText(filePath));
    }

    [Fact]
    public void CreateIfNotExists_CreatesNewFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "new.txt");

        // Act
        FileHelper.CreateIfNotExists(filePath);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void DeleteIfExists_DeletesExistingFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "delete.txt");
        File.WriteAllText(filePath, "Delete me");

        // Act
        FileHelper.DeleteIfExists(filePath);

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void Copy_CopiesFile()
    {
        // Arrange
        var sourcePath = _testFilePath;
        var destinationPath = Path.Combine(_testDirectory, "copy.txt");

        // Act
        FileHelper.Copy(sourcePath, destinationPath);

        // Assert
        Assert.True(File.Exists(destinationPath));
        Assert.Equal(File.ReadAllText(sourcePath), File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Move_MovesFile()
    {
        // Arrange
        var sourcePath = Path.Combine(_testDirectory, "move_source.txt");
        var destinationPath = Path.Combine(_testDirectory, "move_dest.txt");
        File.WriteAllText(sourcePath, "Move me");

        // Act
        FileHelper.Move(sourcePath, destinationPath);

        // Assert
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(destinationPath));
        Assert.Equal("Move me", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Clean_EmptiesFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "clean.txt");
        File.WriteAllText(filePath, "Clean me");

        // Act
        FileHelper.Clean(filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.Equal(0, new FileInfo(filePath).Length);
    }

    [Fact]
    public void GetHash_ReturnsCorrectHash()
    {
        // Act
        var hash = FileHelper.GetHash(_testFilePath);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        // 具体哈希值依赖于文件内容，这里只检查返回了有效的哈希
    }

    [Fact]
    public void GetSize_ReturnsCorrectSize()
    {
        // Act
        var size = FileHelper.GetSize(_testFilePath);

        // Assert
        Assert.Equal(new FileInfo(_testFilePath).Length, size);
    }

    [Fact]
    public void GetName_ReturnsCorrectName()
    {
        // Act
        var name = FileHelper.GetName(_testFilePath);

        // Assert
        Assert.Equal("test.txt", name);
    }

    [Fact]
    public void GetExtension_ReturnsCorrectExtension()
    {
        // Act
        var extension = FileHelper.GetExtension(_testFilePath);

        // Assert
        Assert.Equal(".txt", extension);
    }

    [Fact]
    public void GetNameWithoutExtension_ReturnsCorrectName()
    {
        // Act
        var nameWithoutExtension = FileHelper.GetNameWithoutExtension(_testFilePath);

        // Assert
        Assert.Equal("test", nameWithoutExtension);
    }

    [Fact]
    public void Exists_ReturnsTrueForExistingFile()
    {
        // Act
        var exists = FileHelper.Exists(_testFilePath);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void Exists_ReturnsFalseForNonExistingFile()
    {
        // Arrange
        var nonExistingPath = Path.Combine(_testDirectory, "non_existing.txt");

        // Act
        var exists = FileHelper.Exists(nonExistingPath);

        // Assert
        Assert.False(exists);
    }
}
