#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FtpClientTest
// Guid:bec4d96a-79b7-4c40-9bf3-9ccb9d3fa9ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 7:44:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using FluentFTP;
using Moq;
using XiHan.Framework.Utils.Net.Ftp;

namespace XiHan.Framework.Utils.Test.Net;

/// <summary>
/// FTP客户端测试
/// </summary>
[Trait("Category", "Net")]
public class FtpClientTest
{
    /// <summary>
    /// 测试创建FTP客户端实例成功
    /// </summary>
    [Fact]
    public void FtpClient_Create_Success()
    {
        // Arrange & Act
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");

        // Assert
        Assert.NotNull(ftpClient);
    }

    /// <summary>
    /// 测试上传文件成功
    /// </summary>
    [Fact(Skip = "需要实际FTP服务器连接")]
    public void FtpClient_UploadFile_Success()
    {
        // Arrange
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");
        var localFilePath = Path.GetTempFileName();
        File.WriteAllText(localFilePath, "测试内容");
        var remoteFilePath = $"test_{Guid.NewGuid()}.txt";

        try
        {
            // Act
            var result = ftpClient.UploadFile(localFilePath, remoteFilePath);

            // Assert
            Assert.True(result);
            Assert.True(ftpClient.FileExists(remoteFilePath));
        }
        finally
        {
            // 清理
            if (ftpClient.FileExists(remoteFilePath))
            {
                ftpClient.DeleteFile(remoteFilePath);
            }
            if (File.Exists(localFilePath))
            {
                File.Delete(localFilePath);
            }
            ftpClient.Dispose();
        }
    }

    /// <summary>
    /// 测试下载文件成功
    /// </summary>
    [Fact(Skip = "需要实际FTP服务器连接")]
    public async Task FtpClient_DownloadFile_Success()
    {
        // Arrange
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");
        var localUploadFilePath = Path.GetTempFileName();
        var testContent = "测试内容" + Guid.NewGuid();
        File.WriteAllText(localUploadFilePath, testContent);
        var remoteFilePath = $"test_{Guid.NewGuid()}.txt";
        var localDownloadFilePath = Path.GetTempFileName();

        try
        {
            // 先上传文件
            await ftpClient.UploadFileAsync(localUploadFilePath, remoteFilePath);

            // Act
            var result = await ftpClient.DownloadFileAsync(remoteFilePath, localDownloadFilePath);
            var downloadedContent = File.ReadAllText(localDownloadFilePath);

            // Assert
            Assert.True(result);
            Assert.Equal(testContent, downloadedContent);
        }
        finally
        {
            // 清理
            if (ftpClient.FileExists(remoteFilePath))
            {
                ftpClient.DeleteFile(remoteFilePath);
            }
            if (File.Exists(localUploadFilePath))
            {
                File.Delete(localUploadFilePath);
            }
            if (File.Exists(localDownloadFilePath))
            {
                File.Delete(localDownloadFilePath);
            }
            ftpClient.Dispose();
        }
    }

    /// <summary>
    /// 测试列出目录内容成功
    /// </summary>
    [Fact(Skip = "需要实际FTP服务器连接")]
    public void FtpClient_ListDirectory_Success()
    {
        // Arrange
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");
        var dirName = $"testdir_{Guid.NewGuid()}";
        ftpClient.CreateDirectory(dirName);

        try
        {
            // Act
            var files = ftpClient.ListDirectory();

            // Assert
            Assert.Contains(dirName, files);
        }
        finally
        {
            // 清理
            ftpClient.DeleteDirectory(dirName);
            ftpClient.Dispose();
        }
    }

    /// <summary>
    /// 测试创建和删除目录成功
    /// </summary>
    [Fact(Skip = "需要实际FTP服务器连接")]
    public async Task FtpClient_CreateDeleteDirectory_Success()
    {
        // Arrange
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");
        var dirName = $"testdir_{Guid.NewGuid()}";

        try
        {
            // Act - 创建目录
            var createResult = await ftpClient.CreateDirectoryAsync(dirName);
            var exists = ftpClient.FileExists(dirName);

            // Assert - 创建目录
            Assert.True(createResult);
            Assert.True(exists);

            // Act - 删除目录
            var deleteResult = await ftpClient.DeleteDirectoryAsync(dirName);
            var existsAfterDelete = ftpClient.FileExists(dirName);

            // Assert - 删除目录
            Assert.True(deleteResult);
            Assert.False(existsAfterDelete);
        }
        finally
        {
            // 清理
            if (ftpClient.FileExists(dirName))
            {
                ftpClient.DeleteDirectory(dirName);
            }
            ftpClient.Dispose();
        }
    }

    /// <summary>
    /// 测试重命名文件成功
    /// </summary>
    [Fact(Skip = "需要实际FTP服务器连接")]
    public void FtpClient_RenameFile_Success()
    {
        // Arrange
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");
        var localFilePath = Path.GetTempFileName();
        File.WriteAllText(localFilePath, "测试内容");
        var originalFileName = $"test_original_{Guid.NewGuid()}.txt";
        var newFileName = $"test_renamed_{Guid.NewGuid()}.txt";

        try
        {
            // 上传原始文件
            ftpClient.UploadFile(localFilePath, originalFileName);

            // Act
            var result = ftpClient.Rename(originalFileName, newFileName);
            var originalExists = ftpClient.FileExists(originalFileName);
            var newExists = ftpClient.FileExists(newFileName);

            // Assert
            Assert.True(result);
            Assert.False(originalExists);
            Assert.True(newExists);
        }
        finally
        {
            // 清理
            if (ftpClient.FileExists(originalFileName))
            {
                ftpClient.DeleteFile(originalFileName);
            }
            if (ftpClient.FileExists(newFileName))
            {
                ftpClient.DeleteFile(newFileName);
            }
            if (File.Exists(localFilePath))
            {
                File.Delete(localFilePath);
            }
            ftpClient.Dispose();
        }
    }

    /// <summary>
    /// 测试获取文件大小成功
    /// </summary>
    [Fact(Skip = "需要实际FTP服务器连接")]
    public async Task FtpClient_GetFileSize_Success()
    {
        // Arrange
        var ftpClient = new FtpClient("ftp.example.com", 21, "username", "password");
        var localFilePath = Path.GetTempFileName();
        var content = "This is a test file with known size";
        File.WriteAllText(localFilePath, content);
        var remoteFilePath = $"test_size_{Guid.NewGuid()}.txt";
        var expectedSize = content.Length;

        try
        {
            // 上传文件
            await ftpClient.UploadFileAsync(localFilePath, remoteFilePath);

            // Act
            var size = await ftpClient.GetFileSizeAsync(remoteFilePath);

            // Assert
            Assert.Equal(expectedSize, size);
        }
        finally
        {
            // 清理
            if (ftpClient.FileExists(remoteFilePath))
            {
                ftpClient.DeleteFile(remoteFilePath);
            }
            if (File.Exists(localFilePath))
            {
                File.Delete(localFilePath);
            }
            ftpClient.Dispose();
        }
    }

    /// <summary>
    /// 使用模拟测试上传文件
    /// </summary>
    [Fact]
    public void FtpClient_UploadFile_WithMock_Success()
    {
        // Arrange
        var mockFtpClient = new Mock<FluentFTP.FtpClient>("ftp.example.com", 21, "user", "pass");
        
        // 创建本地测试文件
        var localFilePath = Path.GetTempFileName();
        File.WriteAllText(localFilePath, "测试内容");
        
        try
        {
            // Act & Assert
            // 这里只是验证我们可以创建模拟，实际上不能轻易模拟FluentFTP客户端
            Assert.NotNull(mockFtpClient.Object);
        }
        finally
        {
            // 清理
            if (File.Exists(localFilePath))
            {
                File.Delete(localFilePath);
            }
        }
    }
} 
