using XiHan.Framework.Utils.IO;

namespace XiHan.Framework.Utils.Test.IO;

public class DirectoryHelperTest : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _subDirectory;

    public DirectoryHelperTest()
    {
        // 创建临时测试目录
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DirectoryHelperTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // 创建子目录
        _subDirectory = Path.Combine(_testDirectory, "SubDir");
        Directory.CreateDirectory(_subDirectory);

        // 创建测试文件
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "Hello, World!");
    }

    public void Dispose()
    {
        // 清理测试目录
        if (!Directory.Exists(_testDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // 忽略因文件锁定等导致的删除失败
        }
    }

    [Fact]
    public void CreateIfNotExists_CreatesDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(_testDirectory, "NewDir");

        // Act
        DirectoryHelper.CreateIfNotExists(dirPath);

        // Assert
        Assert.True(Directory.Exists(dirPath));
    }

    [Fact]
    public void CreateIfNotExists_DoesNotThrowIfDirectoryExists()
    {
        // Act & Assert
        var exception = Record.Exception(() => DirectoryHelper.CreateIfNotExists(_testDirectory));
        Assert.Null(exception);
    }

    [Fact]
    public void DeleteIfExists_DeletesDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(_testDirectory, "DirToDelete");
        Directory.CreateDirectory(dirPath);

        // Act
        DirectoryHelper.DeleteIfExists(dirPath);

        // Assert
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public void DeleteIfExists_DoesNotThrowIfDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistingPath = Path.Combine(_testDirectory, "NonExistingDir");

        // Act & Assert
        var exception = Record.Exception(() => DirectoryHelper.DeleteIfExists(nonExistingPath));
        Assert.Null(exception);
    }

    [Fact]
    public void Clear_RemovesAllContents()
    {
        // Arrange
        var dirPath = Path.Combine(_testDirectory, "DirToClear");
        Directory.CreateDirectory(dirPath);

        var subDir = Path.Combine(dirPath, "SubDir");
        Directory.CreateDirectory(subDir);

        var filePath = Path.Combine(dirPath, "file.txt");
        File.WriteAllText(filePath, "Content");

        // Act
        DirectoryHelper.Clear(dirPath);

        // Assert
        Assert.True(Directory.Exists(dirPath)); // 目录本身应该存在
        Assert.Empty(Directory.GetFiles(dirPath)); // 没有文件
        Assert.Empty(Directory.GetDirectories(dirPath)); // 没有子目录
    }

    [Fact]
    public void Copy_CopiesDirectory()
    {
        // Arrange
        var sourcePath = _subDirectory;
        var filePath = Path.Combine(sourcePath, "file.txt");
        File.WriteAllText(filePath, "Content");

        var destPath = Path.Combine(_testDirectory, "CopiedDir");

        // Act
        DirectoryHelper.Copy(sourcePath, destPath);

        // Assert
        Assert.True(Directory.Exists(destPath));
        Assert.True(File.Exists(Path.Combine(destPath, "file.txt")));
        Assert.Equal("Content", File.ReadAllText(Path.Combine(destPath, "file.txt")));
    }

    [Fact]
    public void Move_MovesDirectory()
    {
        // Arrange
        var sourcePath = Path.Combine(_testDirectory, "SourceDir");
        Directory.CreateDirectory(sourcePath);

        var filePath = Path.Combine(sourcePath, "file.txt");
        File.WriteAllText(filePath, "Content");

        var destPath = Path.Combine(_testDirectory, "MovedDir");

        // Act
        DirectoryHelper.Move(sourcePath, destPath);

        // Assert
        Assert.False(Directory.Exists(sourcePath));
        Assert.True(Directory.Exists(destPath));
        Assert.True(File.Exists(Path.Combine(destPath, "file.txt")));
    }

    [Fact]
    public void GetFiles_ReturnsAllFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.txt");
        var file2 = Path.Combine(_testDirectory, "file2.txt");
        File.WriteAllText(file1, "Content1");
        File.WriteAllText(file2, "Content2");

        // Act
        var files = DirectoryHelper.GetFiles(_testDirectory);

        // Assert
        Assert.Equal(3, files.Length); // 包括测试文件
        Assert.Contains(files, f => f.EndsWith("file1.txt"));
        Assert.Contains(files, f => f.EndsWith("file2.txt"));
        Assert.Contains(files, f => f.EndsWith("test.txt"));
    }

    [Fact]
    public void GetFiles_WithPattern_ReturnsMatchingFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "test1.txt");
        var file2 = Path.Combine(_testDirectory, "test2.txt");
        var file3 = Path.Combine(_testDirectory, "other.txt");
        File.WriteAllText(file1, "Content1");
        File.WriteAllText(file2, "Content2");
        File.WriteAllText(file3, "Content3");

        // Act
        var files = DirectoryHelper.GetFiles(_testDirectory, "test*.txt", false);

        // Assert
        Assert.Equal(3, files.Length); // test.txt, test1.txt, test2.txt
        Assert.Contains(files, f => f.EndsWith("test.txt"));
        Assert.Contains(files, f => f.EndsWith("test1.txt"));
        Assert.Contains(files, f => f.EndsWith("test2.txt"));
        Assert.DoesNotContain(files, f => f.EndsWith("other.txt"));
    }

    [Fact]
    public void GetDirectories_ReturnsAllDirectories()
    {
        // Arrange
        var subDir1 = Path.Combine(_testDirectory, "SubDir1");
        var subDir2 = Path.Combine(_testDirectory, "SubDir2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        // Act
        var dirs = DirectoryHelper.GetDirectories(_testDirectory);

        // Assert
        Assert.Equal(3, dirs.Length); // SubDir, SubDir1, SubDir2
        Assert.Contains(dirs, d => d.EndsWith("SubDir"));
        Assert.Contains(dirs, d => d.EndsWith("SubDir1"));
        Assert.Contains(dirs, d => d.EndsWith("SubDir2"));
    }

    [Fact]
    public void GetDirectories_WithPattern_ReturnsMatchingDirectories()
    {
        // Arrange
        var subDir1 = Path.Combine(_testDirectory, "TestDir1");
        var subDir2 = Path.Combine(_testDirectory, "TestDir2");
        var otherDir = Path.Combine(_testDirectory, "OtherDir");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);
        Directory.CreateDirectory(otherDir);

        // Act
        var dirs = DirectoryHelper.GetDirectories(_testDirectory, "Test*", false);

        // Assert
        Assert.Equal(2, dirs.Length);
        Assert.Contains(dirs, d => d.EndsWith("TestDir1"));
        Assert.Contains(dirs, d => d.EndsWith("TestDir2"));
        Assert.DoesNotContain(dirs, d => d.EndsWith("OtherDir"));
    }

    [Fact]
    public void GetSize_ReturnsCorrectSize()
    {
        // Arrange
        var dirPath = Path.Combine(_testDirectory, "SizeDir");
        Directory.CreateDirectory(dirPath);

        var file1 = Path.Combine(dirPath, "file1.txt");
        var file2 = Path.Combine(dirPath, "file2.txt");
        File.WriteAllText(file1, "1234567890");
        File.WriteAllText(file2, "1234567890");

        // Act
        var size = DirectoryHelper.GetSize(dirPath);

        // Assert
        Assert.Equal(20, size); // 2个文件，每个10字节
    }

    [Fact]
    public void GetRandomName_ReturnsNonEmptyString()
    {
        // Act
        var name = DirectoryHelper.GetRandomName();

        // Assert
        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public void Exists_ReturnsTrueForExistingDirectory()
    {
        // Act
        var exists = DirectoryHelper.Exists(_testDirectory);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void Exists_ReturnsFalseForNonExistingDirectory()
    {
        // Arrange
        var nonExistingPath = Path.Combine(_testDirectory, "NonExistingDir");

        // Act
        var exists = DirectoryHelper.Exists(nonExistingPath);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void IsEmpty_ReturnsTrueForEmptyDirectory()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "EmptyDir");
        Directory.CreateDirectory(emptyDir);

        // Act
        var isEmpty = DirectoryHelper.IsEmpty(emptyDir);

        // Assert
        Assert.True(isEmpty);
    }

    [Fact]
    public void IsEmpty_ReturnsFalseForNonEmptyDirectory()
    {
        // Act
        var isEmpty = DirectoryHelper.IsEmpty(_testDirectory);

        // Assert
        Assert.False(isEmpty);
    }
}
