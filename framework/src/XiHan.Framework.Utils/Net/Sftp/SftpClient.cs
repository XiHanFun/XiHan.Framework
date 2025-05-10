#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SftpClient
// Guid:c4d5e6f7-g8h9-i0j1-k2l3-m4n5o6p7q8r9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/7 10:45:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Text;

namespace XiHan.Framework.Utils.Net.Sftp;

/// <summary>
/// SFTP客户端，基于SSH.NET实现
/// </summary>
public class SftpClient : IDisposable
{
    private readonly SftpConnectionInfo _connectionInfo;
    private Renci.SshNet.SftpClient? _client;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// 初始化SFTP客户端
    /// </summary>
    /// <param name="connectionInfo">SFTP连接信息</param>
    public SftpClient(SftpConnectionInfo connectionInfo)
    {
        _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~SftpClient()
    {
        Dispose(false);
    }

    /// <summary>
    /// 连接到SFTP服务器
    /// </summary>
    /// <returns>连接是否成功</returns>
    public bool Connect()
    {
        try
        {
            if (_isConnected)
            {
                return true;
            }

            InitializeClient();
            _client!.Connect();
            _isConnected = _client.IsConnected;
            return _isConnected;
        }
        catch
        {
            _isConnected = false;
            throw;
        }
    }

    /// <summary>
    /// 异步连接到SFTP服务器
    /// </summary>
    /// <returns>连接是否成功</returns>
    public async Task<bool> ConnectAsync()
    {
        return await Task.Run(() => Connect());
    }

    /// <summary>
    /// 断开SFTP连接
    /// </summary>
    public void Disconnect()
    {
        if (_client == null || !_isConnected)
        {
            return;
        }

        _client.Disconnect();
        _isConnected = false;
    }

    /// <summary>
    /// 异步断开SFTP连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        await Task.Run(() => Disconnect());
    }

    /// <summary>
    /// 获取指定目录下的文件和子目录列表
    /// </summary>
    /// <param name="remotePath">远程目录路径</param>
    /// <returns>文件和目录列表</returns>
    public List<SftpFile> ListDirectory(string remotePath)
    {
        EnsureConnected();

        var result = new List<SftpFile>();
        var files = _client!.ListDirectory(remotePath);

        foreach (var file in files)
        {
            // 跳过"."和".."目录
            if (file.Name is "." or "..")
            {
                continue;
            }

            result.Add(new SftpFile
            {
                FullName = file.FullName,
                Name = file.Name,
                Length = file.Length,
                IsDirectory = file.IsDirectory,
                LastWriteTime = file.LastWriteTime,
                LastAccessTime = file.LastAccessTime,
                Permissions = GetPermission(file.Attributes),
                PermissionString = GetPermissionString(file.Attributes),
                UserId = file.UserId,
                GroupId = file.GroupId
            });
        }

        return result;
    }

    /// <summary>
    /// 异步获取指定目录下的文件和子目录列表
    /// </summary>
    /// <param name="remotePath">远程目录路径</param>
    /// <returns>文件和目录列表</returns>
    public async Task<List<SftpFile>> ListDirectoryAsync(string remotePath)
    {
        return await Task.Run(() => ListDirectory(remotePath));
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="localFilePath">本地文件路径</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public void UploadFile(string localFilePath, string remoteFilePath, bool overwrite = true)
    {
        EnsureConnected();

        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("本地文件不存在", localFilePath);
        }

        using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
        UploadFile(fileStream, remoteFilePath, overwrite);
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="input">文件流</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public void UploadFile(Stream input, string remoteFilePath, bool overwrite = true)
    {
        EnsureConnected();

        if (!overwrite && _client!.Exists(remoteFilePath))
        {
            throw new IOException($"远程文件已存在: {remoteFilePath}");
        }

        // 确保目标目录存在
        var remoteDirectory = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/') ?? "";
        if (!string.IsNullOrEmpty(remoteDirectory) && !_client!.Exists(remoteDirectory))
        {
            CreateDirectory(remoteDirectory);
        }

        _client!.UploadFile(input, remoteFilePath, overwrite);
    }

    /// <summary>
    /// 异步上传文件
    /// </summary>
    /// <param name="localFilePath">本地文件路径</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public async Task UploadFileAsync(string localFilePath, string remoteFilePath, bool overwrite = true)
    {
        EnsureConnected();

        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("本地文件不存在", localFilePath);
        }

        using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
        await UploadFileAsync(fileStream, remoteFilePath, overwrite);
    }

    /// <summary>
    /// 异步上传文件
    /// </summary>
    /// <param name="input">文件流</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public async Task UploadFileAsync(Stream input, string remoteFilePath, bool overwrite = true)
    {
        EnsureConnected();

        if (!overwrite && _client!.Exists(remoteFilePath))
        {
            throw new IOException($"远程文件已存在: {remoteFilePath}");
        }

        // 确保目标目录存在
        var remoteDirectory = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/') ?? "";
        if (!string.IsNullOrEmpty(remoteDirectory) && !_client!.Exists(remoteDirectory))
        {
            await CreateDirectoryAsync(remoteDirectory);
        }

        await Task.Run(() => _client!.UploadFile(input, remoteFilePath, overwrite));
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="localFilePath">本地文件路径</param>
    public void DownloadFile(string remoteFilePath, string localFilePath)
    {
        EnsureConnected();

        if (!_client!.Exists(remoteFilePath))
        {
            throw new FileNotFoundException("远程文件不存在", remoteFilePath);
        }

        // 确保本地目录存在
        var localDirectory = Path.GetDirectoryName(localFilePath) ?? "";
        if (!string.IsNullOrEmpty(localDirectory) && !Directory.Exists(localDirectory))
        {
            Directory.CreateDirectory(localDirectory);
        }

        using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
        _client.DownloadFile(remoteFilePath, fileStream);
    }

    /// <summary>
    /// 异步下载文件
    /// </summary>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="localFilePath">本地文件路径</param>
    public async Task DownloadFileAsync(string remoteFilePath, string localFilePath)
    {
        EnsureConnected();

        if (!_client!.Exists(remoteFilePath))
        {
            throw new FileNotFoundException("远程文件不存在", remoteFilePath);
        }

        // 确保本地目录存在
        var localDirectory = Path.GetDirectoryName(localFilePath) ?? "";
        if (!string.IsNullOrEmpty(localDirectory) && !Directory.Exists(localDirectory))
        {
            Directory.CreateDirectory(localDirectory);
        }

        using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
        await Task.Run(() => _client.DownloadFile(remoteFilePath, fileStream));
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="remoteFilePath">远程文件路径</param>
    public void DeleteFile(string remoteFilePath)
    {
        EnsureConnected();

        if (!_client!.Exists(remoteFilePath))
        {
            throw new FileNotFoundException("远程文件不存在", remoteFilePath);
        }

        _client.DeleteFile(remoteFilePath);
    }

    /// <summary>
    /// 异步删除文件
    /// </summary>
    /// <param name="remoteFilePath">远程文件路径</param>
    public async Task DeleteFileAsync(string remoteFilePath)
    {
        EnsureConnected();

        if (!_client!.Exists(remoteFilePath))
        {
            throw new FileNotFoundException("远程文件不存在", remoteFilePath);
        }

        await Task.Run(() => _client.DeleteFile(remoteFilePath));
    }

    /// <summary>
    /// 创建目录
    /// </summary>
    /// <param name="remotePath">远程目录路径</param>
    public void CreateDirectory(string remotePath)
    {
        EnsureConnected();

        // SSH.NET库在创建嵌套目录时有问题，需要自己实现目录层级创建
        var dirs = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var path = "";

        foreach (var dir in dirs)
        {
            path += "/" + dir;
            if (!_client!.Exists(path))
            {
                _client.CreateDirectory(path);
            }
        }
    }

    /// <summary>
    /// 异步创建目录
    /// </summary>
    /// <param name="remotePath">远程目录路径</param>
    public async Task CreateDirectoryAsync(string remotePath)
    {
        await Task.Run(() => CreateDirectory(remotePath));
    }

    /// <summary>
    /// 删除目录
    /// </summary>
    /// <param name="remotePath">远程目录路径</param>
    /// <param name="recursive">是否递归删除</param>
    public void DeleteDirectory(string remotePath, bool recursive = false)
    {
        EnsureConnected();

        if (!_client!.Exists(remotePath))
        {
            throw new DirectoryNotFoundException($"远程目录不存在: {remotePath}");
        }

        if (recursive)
        {
            // 递归删除子目录和文件
            var items = _client.ListDirectory(remotePath);
            foreach (var item in items)
            {
                // 跳过"."和".."目录
                if (item.Name is "." or "..")
                {
                    continue;
                }

                if (item.IsDirectory)
                {
                    DeleteDirectory(item.FullName, true);
                }
                else
                {
                    _client.DeleteFile(item.FullName);
                }
            }
        }

        _client.DeleteDirectory(remotePath);
    }

    /// <summary>
    /// 异步删除目录
    /// </summary>
    /// <param name="remotePath">远程目录路径</param>
    /// <param name="recursive">是否递归删除</param>
    public async Task DeleteDirectoryAsync(string remotePath, bool recursive = false)
    {
        await Task.Run(() => DeleteDirectory(remotePath, recursive));
    }

    /// <summary>
    /// 重命名文件或目录
    /// </summary>
    /// <param name="oldPath">原路径</param>
    /// <param name="newPath">新路径</param>
    public void Rename(string oldPath, string newPath)
    {
        EnsureConnected();

        if (!_client!.Exists(oldPath))
        {
            throw new FileNotFoundException($"文件或目录不存在: {oldPath}");
        }

        _client.RenameFile(oldPath, newPath);
    }

    /// <summary>
    /// 异步重命名文件或目录
    /// </summary>
    /// <param name="oldPath">原路径</param>
    /// <param name="newPath">新路径</param>
    public async Task RenameAsync(string oldPath, string newPath)
    {
        await Task.Run(() => Rename(oldPath, newPath));
    }

    /// <summary>
    /// 读取文件内容为字符串
    /// </summary>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="encoding">字符编码，默认为UTF-8</param>
    /// <returns>文件内容</returns>
    public string ReadAllText(string remoteFilePath, Encoding? encoding = null)
    {
        EnsureConnected();

        if (!_client!.Exists(remoteFilePath))
        {
            throw new FileNotFoundException("远程文件不存在", remoteFilePath);
        }

        using var stream = new MemoryStream();
        _client.DownloadFile(remoteFilePath, stream);
        stream.Position = 0;

        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 异步读取文件内容为字符串
    /// </summary>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="encoding">字符编码，默认为UTF-8</param>
    /// <returns>文件内容</returns>
    public async Task<string> ReadAllTextAsync(string remoteFilePath, Encoding? encoding = null)
    {
        EnsureConnected();

        if (!_client!.Exists(remoteFilePath))
        {
            throw new FileNotFoundException("远程文件不存在", remoteFilePath);
        }

        using var stream = new MemoryStream();
        await Task.Run(() => _client.DownloadFile(remoteFilePath, stream));
        stream.Position = 0;

        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 将字符串写入文件
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="encoding">字符编码，默认为UTF-8</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public void WriteAllText(string content, string remoteFilePath, Encoding? encoding = null, bool overwrite = true)
    {
        EnsureConnected();

        if (!overwrite && _client!.Exists(remoteFilePath))
        {
            throw new IOException($"远程文件已存在: {remoteFilePath}");
        }

        // 确保目标目录存在
        var remoteDirectory = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/') ?? "";
        if (!string.IsNullOrEmpty(remoteDirectory) && !_client!.Exists(remoteDirectory))
        {
            CreateDirectory(remoteDirectory);
        }

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        stream.Position = 0;
        _client!.UploadFile(stream, remoteFilePath, overwrite);
    }

    /// <summary>
    /// 异步将字符串写入文件
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="encoding">字符编码，默认为UTF-8</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public async Task WriteAllTextAsync(string content, string remoteFilePath, Encoding? encoding = null, bool overwrite = true)
    {
        EnsureConnected();

        if (!overwrite && _client!.Exists(remoteFilePath))
        {
            throw new IOException($"远程文件已存在: {remoteFilePath}");
        }

        // 确保目标目录存在
        var remoteDirectory = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/') ?? "";
        if (!string.IsNullOrEmpty(remoteDirectory) && !_client!.Exists(remoteDirectory))
        {
            await CreateDirectoryAsync(remoteDirectory);
        }

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8, leaveOpen: true))
        {
            await writer.WriteAsync(content);
            await writer.FlushAsync();
        }

        stream.Position = 0;
        await Task.Run(() => _client!.UploadFile(stream, remoteFilePath, overwrite));
    }

    /// <summary>
    /// 检查文件或目录是否存在
    /// </summary>
    /// <param name="remotePath">远程路径</param>
    /// <returns>是否存在</returns>
    public bool Exists(string remotePath)
    {
        EnsureConnected();
        return _client!.Exists(remotePath);
    }

    /// <summary>
    /// 异步检查文件或目录是否存在
    /// </summary>
    /// <param name="remotePath">远程路径</param>
    /// <returns>是否存在</returns>
    public async Task<bool> ExistsAsync(string remotePath)
    {
        EnsureConnected();
        return await Task.Run(() => _client!.Exists(remotePath));
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="remotePath">远程路径</param>
    /// <returns>文件信息</returns>
    public SftpFile GetFileInfo(string remotePath)
    {
        EnsureConnected();

        if (!_client!.Exists(remotePath))
        {
            throw new FileNotFoundException($"文件或目录不存在: {remotePath}");
        }

        var fileInfo = _client.GetAttributes(remotePath);

        return new SftpFile
        {
            FullName = remotePath,
            Name = Path.GetFileName(remotePath),
            Length = fileInfo.Size,
            IsDirectory = fileInfo.IsDirectory,
            LastWriteTime = fileInfo.LastWriteTime,
            LastAccessTime = fileInfo.LastAccessTime,
            Permissions = GetPermission(fileInfo),
            PermissionString = GetPermissionString(fileInfo),
            UserId = fileInfo.UserId,
            GroupId = fileInfo.GroupId
        };
    }

    /// <summary>
    /// 异步获取文件信息
    /// </summary>
    /// <param name="remotePath">远程路径</param>
    /// <returns>文件信息</returns>
    public async Task<SftpFile> GetFileInfoAsync(string remotePath)
    {
        return await Task.Run(() => GetFileInfo(remotePath));
    }

    /// <summary>
    /// 设置文件权限
    /// </summary>
    /// <param name="remotePath">远程路径</param>
    /// <param name="permissions">权限（例如：0755）</param>
    public void SetPermissions(string remotePath, int permissions)
    {
        EnsureConnected();

        if (!_client!.Exists(remotePath))
        {
            throw new FileNotFoundException($"文件或目录不存在: {remotePath}");
        }

        var attrs = _client.GetAttributes(remotePath);
        attrs = SetPermission(attrs, permissions);
        _client.SetAttributes(remotePath, attrs);
    }

    /// <summary>
    /// 异步设置文件权限
    /// </summary>
    /// <param name="remotePath">远程路径</param>
    /// <param name="permissions">权限（例如：0755）</param>
    public async Task SetPermissionsAsync(string remotePath, int permissions)
    {
        await Task.Run(() => SetPermissions(remotePath, permissions));
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否正在释放</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Disconnect();
            _client?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// 设置权限，将八进制权限值转换为SftpFileAttributes的布尔属性
    /// </summary>
    /// <param name="attrs">文件属性对象</param>
    /// <param name="permissions">八进制权限值，如0755</param>
    /// <returns>更新后的文件属性对象</returns>
    private static SftpFileAttributes SetPermission(SftpFileAttributes attrs, int permissions)
    {
        // 设置所有者权限 (4-2-1 << 6)
        attrs.OwnerCanRead = (permissions & (4 << 6)) != 0;     // 0400
        attrs.OwnerCanWrite = (permissions & (2 << 6)) != 0;    // 0200
        attrs.OwnerCanExecute = (permissions & (1 << 6)) != 0;  // 0100

        // 设置组权限 (4-2-1 << 3)
        attrs.GroupCanRead = (permissions & (4 << 3)) != 0;     // 0040
        attrs.GroupCanWrite = (permissions & (2 << 3)) != 0;    // 0020
        attrs.GroupCanExecute = (permissions & (1 << 3)) != 0;  // 0010

        // 设置其他人权限 (4-2-1)
        attrs.OthersCanRead = (permissions & 4) != 0;           // 0004
        attrs.OthersCanWrite = (permissions & 2) != 0;          // 0002
        attrs.OthersCanExecute = (permissions & 1) != 0;        // 0001

        return attrs;
    }

    /// <summary>
    /// 计算权限
    /// </summary>
    /// <param name="attrs"></param>
    /// <returns></returns>
    private static int GetPermission(SftpFileAttributes attrs)
    {
        var permissions = 0;

        // 所有者权限
        if (attrs.OwnerCanRead)
        {
            permissions |= 4 << 6;
        }

        if (attrs.OwnerCanWrite)
        {
            permissions |= 2 << 6;
        }

        if (attrs.OwnerCanExecute)
        {
            permissions |= 1 << 6;
        }

        // 组权限
        if (attrs.GroupCanRead)
        {
            permissions |= 4 << 3;
        }

        if (attrs.GroupCanWrite)
        {
            permissions |= 2 << 3;
        }

        if (attrs.GroupCanExecute)
        {
            permissions |= 1 << 3;
        }

        // 其他人权限
        if (attrs.OthersCanRead)
        {
            permissions |= 4;
        }

        if (attrs.OthersCanWrite)
        {
            permissions |= 2;
        }

        if (attrs.OthersCanExecute)
        {
            permissions |= 1;
        }

        return permissions;
    }

    /// <summary>
    /// 根据布尔属性创建权限字符串（如：-rwxr-xr-x）
    /// </summary>
    /// <param name="attrs"></param>
    /// <returns></returns>
    private static string GetPermissionString(SftpFileAttributes attrs)
    {
        var result = new char[10];

        // 文件类型（这里需要根据实际情况调整，因为我们没有IsDirectory属性）
        result[0] = attrs.IsDirectory ? 'd' : '-';

        // 所有者权限
        result[1] = attrs.OwnerCanRead ? 'r' : '-';
        result[2] = attrs.OwnerCanWrite ? 'w' : '-';
        result[3] = attrs.OwnerCanExecute ? 'x' : '-';

        // 组权限
        result[4] = attrs.GroupCanRead ? 'r' : '-';
        result[5] = attrs.GroupCanWrite ? 'w' : '-';
        result[6] = attrs.GroupCanExecute ? 'x' : '-';

        // 其他人权限
        result[7] = attrs.OthersCanRead ? 'r' : '-';
        result[8] = attrs.OthersCanWrite ? 'w' : '-';
        result[9] = attrs.OthersCanExecute ? 'x' : '-';

        return new string(result);
    }

    /// <summary>
    /// 初始化SFTP客户端
    /// </summary>
    private void InitializeClient()
    {
        if (_client != null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_connectionInfo.PrivateKeyPath))
        {
            // 使用私钥认证
            var privateKeyFile = new PrivateKeyFile(_connectionInfo.PrivateKeyPath, _connectionInfo.PrivateKeyPassphrase);
            _client = new Renci.SshNet.SftpClient(
                _connectionInfo.Host,
                _connectionInfo.Port,
                _connectionInfo.Username,
                privateKeyFile);
        }
        else
        {
            // 使用密码认证
            _client = new Renci.SshNet.SftpClient(
                _connectionInfo.Host,
                _connectionInfo.Port,
                _connectionInfo.Username,
                _connectionInfo.Password);
        }

        // 设置操作超时
        _client.OperationTimeout = TimeSpan.FromMilliseconds(_connectionInfo.ConnectionTimeout);
        _client.ConnectionInfo.Timeout = TimeSpan.FromMilliseconds(_connectionInfo.ConnectionTimeout);
        _client.ConnectionInfo.Encoding = Encoding.UTF8;
        _client.BufferSize = 32 * 1024; // 32KB缓冲区
    }

    /// <summary>
    /// 确保SFTP已连接
    /// </summary>
    private void EnsureConnected()
    {
        if (!_isConnected || _client == null || !_client.IsConnected)
        {
            Connect();
        }
    }
}
