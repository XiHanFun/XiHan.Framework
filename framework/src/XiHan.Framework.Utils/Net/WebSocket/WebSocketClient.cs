#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WebSocketClient
// Guid:38a6c1d2-5a56-4b0e-9b81-f9e2e8a3d78c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 07:44:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace XiHan.Framework.Utils.Net.WebSocket;

/// <summary>
/// WebSocket客户端，基于ClientWebSocket实现
/// </summary>
public class WebSocketClient : IDisposable
{
    private readonly Uri _uri;
    private readonly ClientWebSocket _webSocket;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="url">WebSocket URL</param>
    public WebSocketClient(string url)
    {
        _uri = new Uri(url);
        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// 接收消息事件
    /// </summary>
    public event EventHandler<WebSocketEventArgs>? OnMessage;

    /// <summary>
    /// 连接建立事件
    /// </summary>
    public event EventHandler? OnOpen;

    /// <summary>
    /// 连接关闭事件
    /// </summary>
    public event EventHandler<WebSocketCloseEventArgs>? OnClose;

    /// <summary>
    /// 错误事件
    /// </summary>
    public event EventHandler<WebSocketErrorEventArgs>? OnError;

    /// <summary>
    /// WebSocket连接状态
    /// </summary>
    public WebSocketState State => _webSocket.State;

    /// <summary>
    /// 添加子协议
    /// </summary>
    /// <param name="subProtocol">子协议</param>
    public void AddSubProtocol(string subProtocol)
    {
        _webSocket.Options.AddSubProtocol(subProtocol);
    }

    /// <summary>
    /// 设置请求头
    /// </summary>
    /// <param name="name">请求头名称</param>
    /// <param name="value">请求头值</param>
    public void SetRequestHeader(string name, string value)
    {
        _webSocket.Options.SetRequestHeader(name, value);
    }

    /// <summary>
    /// 设置连接超时时间
    /// </summary>
    /// <param name="timeout">超时时间(秒)</param>
    public void SetConnectTimeout(int timeout)
    {
        _webSocket.Options.HttpVersion = HttpVersion.Version11;
        _webSocket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        _webSocket.Options.DangerousDeflateOptions = null;
        _webSocket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(timeout);
    }

    /// <summary>
    /// 连接到WebSocket服务器
    /// </summary>
    /// <returns>连接结果</returns>
    public async Task<bool> ConnectAsync()
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            return true;
        }

        if (_webSocket.State is not WebSocketState.None and not WebSocketState.Closed)
        {
            return false;
        }

        try
        {
            await _webSocket.ConnectAsync(_uri, _cancellationTokenSource.Token);
            _isRunning = true;

            // 触发连接建立事件
            OnOpen?.Invoke(this, EventArgs.Empty);

            // 启动接收任务
            await Task.Run(ReceiveLoop);

            return true;
        }
        catch (Exception ex)
        {
            // 触发错误事件
            OnError?.Invoke(this, new WebSocketErrorEventArgs(ex));
            return false;
        }
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <returns>发送结果</returns>
    public async Task<bool> SendTextAsync(string message)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            return false;
        }

        try
        {
            // 使用信号量确保同一时间只有一个发送操作
            await _sendLock.WaitAsync();

            var buffer = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            // 触发错误事件
            OnError?.Invoke(this, new WebSocketErrorEventArgs(ex));
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 发送二进制消息
    /// </summary>
    /// <param name="data">二进制数据</param>
    /// <returns>发送结果</returns>
    public async Task<bool> SendBinaryAsync(byte[] data)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            return false;
        }

        try
        {
            // 使用信号量确保同一时间只有一个发送操作
            await _sendLock.WaitAsync();

            await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            // 触发错误事件
            OnError?.Invoke(this, new WebSocketErrorEventArgs(ex));
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 关闭WebSocket连接
    /// </summary>
    /// <param name="closeStatus">关闭状态码</param>
    /// <param name="statusDescription">关闭描述</param>
    /// <returns>关闭结果</returns>
    public async Task<bool> CloseAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string? statusDescription = null)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            return false;
        }

        try
        {
            // 关闭连接
            await _webSocket.CloseAsync(closeStatus, statusDescription, _cancellationTokenSource.Token);

            // 触发关闭事件
            OnClose?.Invoke(this, new WebSocketCloseEventArgs(closeStatus, statusDescription, false));

            return true;
        }
        catch (Exception ex)
        {
            // 触发错误事件
            OnError?.Invoke(this, new WebSocketErrorEventArgs(ex));
            return false;
        }
        finally
        {
            _isRunning = false;
        }
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
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // 释放托管资源
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _sendLock.Dispose();
            _webSocket.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// 接收消息循环
    /// </summary>
    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();

        try
        {
            while (_isRunning && _webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                messageBuffer.Clear();

                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // 处理远程关闭
                        await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _cancellationTokenSource.Token);
                        OnClose?.Invoke(this, new WebSocketCloseEventArgs(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, true));
                        _isRunning = false;
                        break;
                    }

                    // 将接收到的数据添加到消息缓冲区
                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                // 如果连接已关闭，退出循环
                if (!_isRunning || _webSocket.State != WebSocketState.Open)
                {
                    break;
                }

                switch (result.MessageType)
                {
                    // 处理完整消息
                    case WebSocketMessageType.Text when messageBuffer.Count > 0:
                        {
                            var message = Encoding.UTF8.GetString([.. messageBuffer]);
                            OnMessage?.Invoke(this, new WebSocketEventArgs(message));
                            break;
                        }
                    case WebSocketMessageType.Binary:
                        {
                            // 处理二进制消息
                            // 将二进制数据转换为Base64字符串传递
                            if (messageBuffer.Count > 0)
                            {
                                var base64 = Convert.ToBase64String(messageBuffer.ToArray());
                                OnMessage?.Invoke(this, new WebSocketEventArgs($"BINARY:{base64}"));
                            }

                            break;
                        }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 取消操作，正常退出
        }
        catch (Exception ex)
        {
            // 触发错误事件
            OnError?.Invoke(this, new WebSocketErrorEventArgs(ex));
        }
        finally
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                // 确保连接已关闭
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Connection lost", CancellationToken.None);
                }
                catch
                {
                    // 忽略关闭过程中的错误
                }
            }

            _isRunning = false;
        }
    }
}
