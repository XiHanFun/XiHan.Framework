#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SshClient
// Guid:b1c2d3e4-f5g6-h7i8-j9k0-l1m2n3o4p5q6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/7 10:30:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Renci.SshNet;
using System.Text;

namespace XiHan.Framework.Utils.Net.Ssh;

/// <summary>
/// SSH客户端，基于SSH.NET实现
/// </summary>
public class SshClient : IDisposable
{
    private readonly SshConnectionInfo _connectionInfo;
    private Renci.SshNet.SshClient? _client;
    private ShellStream? _shellStream;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// 初始化SSH客户端
    /// </summary>
    /// <param name="connectionInfo">SSH连接信息</param>
    public SshClient(SshConnectionInfo connectionInfo)
    {
        _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~SshClient()
    {
        Dispose(false);
    }

    /// <summary>
    /// 连接到SSH服务器
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
    /// 异步连接到SSH服务器
    /// </summary>
    /// <returns>连接是否成功</returns>
    public async Task<bool> ConnectAsync()
    {
        return await Task.Run(() => Connect());
    }

    /// <summary>
    /// 断开SSH连接
    /// </summary>
    public void Disconnect()
    {
        CloseShellStream();

        if (_client != null && _isConnected)
        {
            _client.Disconnect();
            _isConnected = false;
        }
    }

    /// <summary>
    /// 异步断开SSH连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        await Task.Run(() => Disconnect());
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <returns>命令执行结果</returns>
    public SshCommandResult ExecuteCommand(string command)
    {
        EnsureConnected();

        using var cmd = _client!.CreateCommand(command);
        cmd.CommandTimeout = TimeSpan.FromMilliseconds(_connectionInfo.ConnectionTimeout);

        var result = cmd.Execute();

        return new SshCommandResult
        {
            Command = command,
            Result = result,
            Error = cmd.Error,
            ExitCode = cmd.ExitStatus ?? -1
        };
    }

    /// <summary>
    /// 异步执行命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <returns>命令执行结果</returns>
    public async Task<SshCommandResult> ExecuteCommandAsync(string command)
    {
        EnsureConnected();

        using var cmd = _client!.CreateCommand(command);
        cmd.CommandTimeout = TimeSpan.FromMilliseconds(_connectionInfo.ConnectionTimeout);

        await cmd.ExecuteAsync();

        return new SshCommandResult
        {
            Command = command,
            Result = cmd.Result,
            Error = cmd.Error,
            ExitCode = cmd.ExitStatus ?? -1
        };
    }

    /// <summary>
    /// 启动交互式Shell会话
    /// </summary>
    /// <param name="terminalName">终端类型，默认为xterm</param>
    /// <param name="columns">列数</param>
    /// <param name="rows">行数</param>
    public void StartShell(string terminalName = "xterm", uint columns = 80, uint rows = 24)
    {
        EnsureConnected();
        CloseShellStream();

        _shellStream = _client!.CreateShellStream(terminalName, columns, rows, columns, rows, 1024);
    }

    /// <summary>
    /// 在Shell会话中发送命令
    /// </summary>
    /// <param name="command">命令</param>
    /// <param name="expectValue">期望的返回值，用于判断命令是否执行完成</param>
    /// <param name="timeout">超时时间(毫秒)</param>
    /// <returns>命令输出</returns>
    public string SendShellCommand(string command, string expectValue, int timeout = 30000)
    {
        if (_shellStream == null)
        {
            StartShell();
        }

        // 清空缓冲区
        _shellStream!.Flush();

        // 发送命令
        _shellStream.WriteLine(command);

        // 等待响应并读取输出
        var output = new StringBuilder();
        var startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
        {
            if (_shellStream.DataAvailable)
            {
                var buffer = new byte[1024];
                var read = _shellStream.Read(buffer, 0, buffer.Length);
                var data = Encoding.UTF8.GetString(buffer, 0, read);
                output.Append(data);

                // 检查是否包含期望的返回值
                if (!string.IsNullOrEmpty(expectValue) && output.ToString().Contains(expectValue))
                {
                    break;
                }
            }
            else
            {
                // 暂停一小段时间，避免CPU占用过高
                Thread.Sleep(100);
            }
        }

        // 检查是否超时
        return !output.ToString().Contains(expectValue) && !string.IsNullOrEmpty(expectValue)
            ? throw new TimeoutException($"等待命令响应超时: {command}")
            : output.ToString();
    }

    /// <summary>
    /// 异步在Shell会话中发送命令
    /// </summary>
    /// <param name="command">命令</param>
    /// <param name="expectValue">期望的返回值，用于判断命令是否执行完成</param>
    /// <param name="timeout">超时时间(毫秒)</param>
    /// <returns>命令输出</returns>
    public async Task<string> SendShellCommandAsync(string command, string expectValue, int timeout = 30000)
    {
        return await Task.Run(() => SendShellCommand(command, expectValue, timeout));
    }

    /// <summary>
    /// 关闭Shell会话
    /// </summary>
    public void CloseShellStream()
    {
        if (_shellStream != null)
        {
            _shellStream.Close();
            _shellStream.Dispose();
            _shellStream = null;
        }
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="localPath">本地文件路径</param>
    /// <param name="remotePath">远程文件路径</param>
    public void UploadFile(string localPath, string remotePath)
    {
        EnsureConnected();

        using var scp = new ScpClient(_connectionInfo.Host, _connectionInfo.Port, _connectionInfo.Username, _connectionInfo.Password);
        scp.Connect();
        scp.Upload(new FileInfo(localPath), remotePath);
        scp.Disconnect();
    }

    /// <summary>
    /// 异步上传文件
    /// </summary>
    /// <param name="localPath">本地文件路径</param>
    /// <param name="remotePath">远程文件路径</param>
    public async Task UploadFileAsync(string localPath, string remotePath)
    {
        EnsureConnected();

        using var scp = new ScpClient(_connectionInfo.Host, _connectionInfo.Port, _connectionInfo.Username, _connectionInfo.Password);
        await Task.Run(() =>
        {
            scp.Connect();
            scp.Upload(new FileInfo(localPath), remotePath);
            scp.Disconnect();
        });
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="remotePath">远程文件路径</param>
    /// <param name="localPath">本地文件路径</param>
    public void DownloadFile(string remotePath, string localPath)
    {
        EnsureConnected();

        using var scp = new ScpClient(_connectionInfo.Host, _connectionInfo.Port, _connectionInfo.Username, _connectionInfo.Password);
        scp.Connect();
        scp.Download(remotePath, new FileInfo(localPath));
        scp.Disconnect();
    }

    /// <summary>
    /// 异步下载文件
    /// </summary>
    /// <param name="remotePath">远程文件路径</param>
    /// <param name="localPath">本地文件路径</param>
    public async Task DownloadFileAsync(string remotePath, string localPath)
    {
        EnsureConnected();

        using var scp = new ScpClient(_connectionInfo.Host, _connectionInfo.Port, _connectionInfo.Username, _connectionInfo.Password);
        await Task.Run(() =>
        {
            scp.Connect();
            scp.Download(remotePath, new FileInfo(localPath));
            scp.Disconnect();
        });
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
            CloseShellStream();
            Disconnect();
            _client?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// 初始化SSH客户端
    /// </summary>
    private void InitializeClient()
    {
        if (_client != null)
        {
            return;
        }

        ConnectionInfo connectionInfo;

        if (!string.IsNullOrEmpty(_connectionInfo.PrivateKeyPath))
        {
            // 使用私钥认证
            var privateKeyFile = new PrivateKeyFile(_connectionInfo.PrivateKeyPath, _connectionInfo.PrivateKeyPassphrase);
            connectionInfo = new ConnectionInfo(
                _connectionInfo.Host,
                _connectionInfo.Port,
                _connectionInfo.Username,
                new PrivateKeyAuthenticationMethod(_connectionInfo.Username, privateKeyFile));
        }
        else
        {
            // 使用密码认证
            connectionInfo = new ConnectionInfo(
                _connectionInfo.Host,
                _connectionInfo.Port,
                _connectionInfo.Username,
                new PasswordAuthenticationMethod(_connectionInfo.Username, _connectionInfo.Password));
        }

        // 应用连接设置
        connectionInfo.Timeout = TimeSpan.FromMilliseconds(_connectionInfo.ConnectionTimeout);
        connectionInfo.Encoding = Encoding.UTF8;
        connectionInfo.RetryAttempts = 3;

        _client = new Renci.SshNet.SshClient(connectionInfo);

        if (_connectionInfo.UseCompression)
        {
            _client.ConnectionInfo.CompressionAlgorithms.Clear();
            _client.ConnectionInfo.CompressionAlgorithms.Add("zlib@openssh.com", () => new Renci.SshNet.Compression.ZlibOpenSsh());
        }
    }

    /// <summary>
    /// 确保SSH已连接
    /// </summary>
    private void EnsureConnected()
    {
        if (!_isConnected || _client == null || !_client.IsConnected)
        {
            Connect();
        }
    }
}
