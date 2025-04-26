#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SftpClientTest
// Guid:b0e47a69-1520-4fd8-9b3c-e0a6c487b561
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 9:25:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using Xunit;
using XiHan.Framework.Utils.Net.Sftp;

namespace XiHan.Framework.Utils.Test.Net.Sftp;

/// <summary>
/// SFTP客户端测试类
/// </summary>
public class SftpClientTest
{
    // 测试连接信息，请在实际运行测试前替换为可用的SFTP服务器信息
    private SftpConnectionInfo GetTestConnectionInfo()
    {
        // 注意：这里使用了测试服务器信息，实际使用时需替换为有效的SFTP服务器
        return new SftpConnectionInfo(
            host: "test.rebex.net",
            port: 22,
            username: "demo",
            password: "password");
    }

    /// <summary>
    /// 测试创建SftpClient实例
    /// </summary>
    [Fact]
    public void SftpClient_Create_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();

        // Act
        using var client = new SftpClient(connectionInfo);

        // Assert
        Assert.NotNull(client);
    }

    /// <summary>
    /// 测试连接到SFTP服务器
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_Connect_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);

        try
        {
            // Act
            client.Connect();

            // Assert
            Assert.True(client.IsConnected);
        }
        finally
        {
            // 确保断开连接
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试连接到SFTP服务器(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_ConnectAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);

        try
        {
            // Act
            await client.ConnectAsync();

            // Assert
            Assert.True(client.IsConnected);
        }
        finally
        {
            // 确保断开连接
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试列出目录内容
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_ListDirectory_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);

        try
        {
            client.Connect();

            // Act
            var files = client.ListDirectory("/");

            // Assert
            Assert.NotNull(files);
            Assert.NotEmpty(files);
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试列出目录内容(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_ListDirectoryAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);

        try
        {
            await client.ConnectAsync();

            // Act
            var files = await client.ListDirectoryAsync("/");

            // Assert
            Assert.NotNull(files);
            Assert.NotEmpty(files);
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试文件上传下载
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_UploadDownloadFile_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var localUploadPath = Path.GetTempFileName();
        var localDownloadPath = Path.GetTempFileName();
        var testContent = "This is a test file content";

        try
        {
            // 创建测试文件
            File.WriteAllText(localUploadPath, testContent);

            // 连接
            client.Connect();

            // Act - 上传文件
            client.UploadFile(localUploadPath, remoteFilePath);

            // Act - 下载文件
            client.DownloadFile(remoteFilePath, localDownloadPath);

            // Assert - 检查下载的文件内容
            var downloadedContent = File.ReadAllText(localDownloadPath);
            Assert.Equal(testContent, downloadedContent);

            // Act - 删除远程文件
            client.DeleteFile(remoteFilePath);
        }
        finally
        {
            // 清理本地测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            if (File.Exists(localDownloadPath))
            {
                File.Delete(localDownloadPath);
            }

            // 确保远程文件被删除
            if (client.IsConnected)
            {
                try { client.DeleteFile(remoteFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试文件上传下载(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_UploadDownloadFileAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var localUploadPath = Path.GetTempFileName();
        var localDownloadPath = Path.GetTempFileName();
        var testContent = "This is a test file content";

        try
        {
            // 创建测试文件
            await File.WriteAllTextAsync(localUploadPath, testContent);

            // 连接
            await client.ConnectAsync();

            // Act - 上传文件
            await client.UploadFileAsync(localUploadPath, remoteFilePath);

            // Act - 下载文件
            await client.DownloadFileAsync(remoteFilePath, localDownloadPath);

            // Assert - 检查下载的文件内容
            var downloadedContent = await File.ReadAllTextAsync(localDownloadPath);
            Assert.Equal(testContent, downloadedContent);

            // Act - 删除远程文件
            await client.DeleteFileAsync(remoteFilePath);
        }
        finally
        {
            // 清理本地测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            if (File.Exists(localDownloadPath))
            {
                File.Delete(localDownloadPath);
            }

            // 确保远程文件被删除
            if (client.IsConnected)
            {
                try { await client.DeleteFileAsync(remoteFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试目录创建和删除
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_CreateDeleteDirectory_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testDirName = $"test_dir_{Guid.NewGuid()}";
        var remoteDirPath = $"/{testDirName}";

        try
        {
            // 连接
            client.Connect();

            // Act - 创建目录
            client.CreateDirectory(remoteDirPath);

            // Act - 检查目录是否存在
            var files = client.ListDirectory("/");
            var dirExists = files.Any(f => f.Name == testDirName);
            Assert.True(dirExists);

            // Act - 删除目录
            client.DeleteDirectory(remoteDirPath);

            // Act - 确认目录已删除
            files = client.ListDirectory("/");
            dirExists = files.Any(f => f.Name == testDirName);
            Assert.False(dirExists);
        }
        finally
        {
            // 确保测试目录被删除
            if (client.IsConnected)
            {
                try { client.DeleteDirectory(remoteDirPath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试目录创建和删除(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_CreateDeleteDirectoryAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testDirName = $"test_dir_{Guid.NewGuid()}";
        var remoteDirPath = $"/{testDirName}";

        try
        {
            // 连接
            await client.ConnectAsync();

            // Act - 创建目录
            await client.CreateDirectoryAsync(remoteDirPath);

            // Act - 检查目录是否存在
            var files = await client.ListDirectoryAsync("/");
            var dirExists = files.Any(f => f.Name == testDirName);
            Assert.True(dirExists);

            // Act - 删除目录
            await client.DeleteDirectoryAsync(remoteDirPath);

            // Act - 确认目录已删除
            files = await client.ListDirectoryAsync("/");
            dirExists = files.Any(f => f.Name == testDirName);
            Assert.False(dirExists);
        }
        finally
        {
            // 确保测试目录被删除
            if (client.IsConnected)
            {
                try { await client.DeleteDirectoryAsync(remoteDirPath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试文件重命名
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_RenameFile_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var renamedFileName = $"renamed_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var renamedFilePath = $"/{renamedFileName}";
        var localUploadPath = Path.GetTempFileName();

        try
        {
            // 创建测试文件
            File.WriteAllText(localUploadPath, "Test content");

            // 连接并上传文件
            client.Connect();
            client.UploadFile(localUploadPath, remoteFilePath);

            // Act - 重命名文件
            client.RenameFile(remoteFilePath, renamedFilePath);

            // Assert - 检查文件是否已重命名
            var files = client.ListDirectory("/");
            Assert.DoesNotContain(files, f => f.Name == testFileName);
            Assert.Contains(files, f => f.Name == renamedFileName);

            // 清理 - 删除重命名后的文件
            client.DeleteFile(renamedFilePath);
        }
        finally
        {
            // 清理测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            // 删除远程文件
            if (client.IsConnected)
            {
                try { client.DeleteFile(remoteFilePath); } catch { /* 忽略错误 */ }
                try { client.DeleteFile(renamedFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试文件重命名(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_RenameFileAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var renamedFileName = $"renamed_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var renamedFilePath = $"/{renamedFileName}";
        var localUploadPath = Path.GetTempFileName();

        try
        {
            // 创建测试文件
            await File.WriteAllTextAsync(localUploadPath, "Test content");

            // 连接并上传文件
            await client.ConnectAsync();
            await client.UploadFileAsync(localUploadPath, remoteFilePath);

            // Act - 重命名文件
            await client.RenameFileAsync(remoteFilePath, renamedFilePath);

            // Assert - 检查文件是否已重命名
            var files = await client.ListDirectoryAsync("/");
            Assert.DoesNotContain(files, f => f.Name == testFileName);
            Assert.Contains(files, f => f.Name == renamedFileName);

            // 清理 - 删除重命名后的文件
            await client.DeleteFileAsync(renamedFilePath);
        }
        finally
        {
            // 清理测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            // 删除远程文件
            if (client.IsConnected)
            {
                try { await client.DeleteFileAsync(remoteFilePath); } catch { /* 忽略错误 */ }
                try { await client.DeleteFileAsync(renamedFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试检查文件是否存在
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_FileExists_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var nonExistentFilePath = $"/nonexistent_{Guid.NewGuid()}.txt";
        var localUploadPath = Path.GetTempFileName();

        try
        {
            // 创建测试文件
            File.WriteAllText(localUploadPath, "Test content");

            // 连接并上传文件
            client.Connect();
            client.UploadFile(localUploadPath, remoteFilePath);

            // Act & Assert - 检查文件是否存在
            Assert.True(client.FileExists(remoteFilePath));
            Assert.False(client.FileExists(nonExistentFilePath));

            // 清理 - 删除文件
            client.DeleteFile(remoteFilePath);
        }
        finally
        {
            // 清理测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            // 确保远程文件被删除
            if (client.IsConnected)
            {
                try { client.DeleteFile(remoteFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试检查文件是否存在(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_FileExistsAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var nonExistentFilePath = $"/nonexistent_{Guid.NewGuid()}.txt";
        var localUploadPath = Path.GetTempFileName();

        try
        {
            // 创建测试文件
            await File.WriteAllTextAsync(localUploadPath, "Test content");

            // 连接并上传文件
            await client.ConnectAsync();
            await client.UploadFileAsync(localUploadPath, remoteFilePath);

            // Act & Assert - 检查文件是否存在
            Assert.True(await client.FileExistsAsync(remoteFilePath));
            Assert.False(await client.FileExistsAsync(nonExistentFilePath));

            // 清理 - 删除文件
            await client.DeleteFileAsync(remoteFilePath);
        }
        finally
        {
            // 清理测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            // 确保远程文件被删除
            if (client.IsConnected)
            {
                try { await client.DeleteFileAsync(remoteFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试获取文件信息
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public void SftpClient_GetFileInfo_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var localUploadPath = Path.GetTempFileName();
        var testContent = "This is a test file content";

        try
        {
            // 创建测试文件
            File.WriteAllText(localUploadPath, testContent);
            var expectedSize = new FileInfo(localUploadPath).Length;

            // 连接并上传文件
            client.Connect();
            client.UploadFile(localUploadPath, remoteFilePath);

            // Act - 获取文件信息
            var fileInfo = client.GetFileInfo(remoteFilePath);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.Equal(testFileName, fileInfo.Name);
            Assert.Equal(expectedSize, fileInfo.Length);
            Assert.False(fileInfo.IsDirectory);

            // 清理 - 删除文件
            client.DeleteFile(remoteFilePath);
        }
        finally
        {
            // 清理测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            // 确保远程文件被删除
            if (client.IsConnected)
            {
                try { client.DeleteFile(remoteFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }

    /// <summary>
    /// 测试获取文件信息(异步)
    /// </summary>
    [Fact(Skip = "需要有效的SFTP服务器连接")]
    public async Task SftpClient_GetFileInfoAsync_Success()
    {
        // Arrange
        var connectionInfo = GetTestConnectionInfo();
        using var client = new SftpClient(connectionInfo);
        var testFileName = $"test_{Guid.NewGuid()}.txt";
        var remoteFilePath = $"/{testFileName}";
        var localUploadPath = Path.GetTempFileName();
        var testContent = "This is a test file content";

        try
        {
            // 创建测试文件
            await File.WriteAllTextAsync(localUploadPath, testContent);
            var expectedSize = new FileInfo(localUploadPath).Length;

            // 连接并上传文件
            await client.ConnectAsync();
            await client.UploadFileAsync(localUploadPath, remoteFilePath);

            // Act - 获取文件信息
            var fileInfo = await client.GetFileInfoAsync(remoteFilePath);

            // Assert
            Assert.NotNull(fileInfo);
            Assert.Equal(testFileName, fileInfo.Name);
            Assert.Equal(expectedSize, fileInfo.Length);
            Assert.False(fileInfo.IsDirectory);

            // 清理 - 删除文件
            await client.DeleteFileAsync(remoteFilePath);
        }
        finally
        {
            // 清理测试文件
            if (File.Exists(localUploadPath))
            {
                File.Delete(localUploadPath);
            }

            // 确保远程文件被删除
            if (client.IsConnected)
            {
                try { await client.DeleteFileAsync(remoteFilePath); } catch { /* 忽略错误 */ }
                client.Disconnect();
            }
        }
    }
} 
