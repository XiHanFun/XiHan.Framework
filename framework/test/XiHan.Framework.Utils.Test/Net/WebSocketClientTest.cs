#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WebSocketClientTest
// Guid:74a92e83-5d61-4b96-8cfc-1e12a0a49873
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 9:10:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net.WebSockets;
using System.Text;
using Xunit;
using XiHan.Framework.Utils.Net.WebSocket;

namespace XiHan.Framework.Utils.Test.Net;

/// <summary>
/// WebSocket客户端测试
/// </summary>
public class WebSocketClientTest
{
    // 测试服务器，可通过https://www.piesocket.com/websocket-tester获取测试地址
    private const string TestWebSocketUrl = "wss://demo.piesocket.com/v3/channel_123?api_key=VCXCEuvhGcBDP7XhiJJUDvR1e1D3eiVjgZ9VRiaV&notify_self=1";

    /// <summary>
    /// 测试创建WebSocketClient实例
    /// </summary>
    [Fact]
    public void WebSocketClient_Create_Success()
    {
        // Arrange & Act
        var client = new WebSocketClient(TestWebSocketUrl);

        // Assert
        Assert.NotNull(client);
        Assert.Equal(WebSocketState.None, client.State);
    }

    /// <summary>
    /// 测试添加子协议
    /// </summary>
    [Fact]
    public void WebSocketClient_AddSubProtocol_Success()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);

        // Act & Assert - 不会抛出异常
        client.AddSubProtocol("test-protocol");
    }

    /// <summary>
    /// 测试设置请求头
    /// </summary>
    [Fact]
    public void WebSocketClient_SetRequestHeader_Success()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);

        // Act & Assert - 不会抛出异常
        client.SetRequestHeader("X-Test-Header", "test-value");
    }

    /// <summary>
    /// 测试设置连接超时时间
    /// </summary>
    [Fact]
    public void WebSocketClient_SetConnectTimeout_Success()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);

        // Act & Assert - 不会抛出异常
        client.SetConnectTimeout(30);
    }

    /// <summary>
    /// 测试连接、发送消息和关闭连接的集成测试
    /// 注意：此测试需要网络连接和可用的WebSocket服务器
    /// </summary>
    [Fact(Skip = "需要可用的WebSocket服务器")]
    public async Task WebSocketClient_ConnectSendClose_Success()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);
        var receivedMessages = new List<string>();
        var connectionOpened = false;
        var connectionClosed = false;
        
        client.OnOpen += (sender, args) => connectionOpened = true;
        client.OnMessage += (sender, args) => receivedMessages.Add(args.Message);
        client.OnClose += (sender, args) => connectionClosed = true;
        
        try
        {
            // Act - 连接
            var connectResult = await client.ConnectAsync();
            Assert.True(connectResult);
            Assert.Equal(WebSocketState.Open, client.State);
            Assert.True(connectionOpened);
            
            // 等待连接稳定
            await Task.Delay(500);
            
            // Act - 发送消息
            var testMessage = $"Test message: {Guid.NewGuid()}";
            var sendResult = await client.SendTextAsync(testMessage);
            Assert.True(sendResult);
            
            // 等待消息被回显
            await Task.Delay(1000);
            
            // Act - 关闭连接
            var closeResult = await client.CloseAsync();
            Assert.True(closeResult);
            
            // 等待连接关闭
            await Task.Delay(500);
            
            // Assert
            Assert.Contains(receivedMessages, msg => msg.Contains(testMessage));
            Assert.True(connectionClosed);
            Assert.Equal(WebSocketState.Closed, client.State);
        }
        finally
        {
            // 清理资源
            client.Dispose();
        }
    }

    /// <summary>
    /// 测试二进制消息发送
    /// </summary>
    [Fact(Skip = "需要可用的WebSocket服务器")]
    public async Task WebSocketClient_SendBinary_Success()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);
        var binaryReceived = false;
        
        client.OnMessage += (sender, args) => 
        {
            if (args.Message.StartsWith("BINARY:"))
            {
                binaryReceived = true;
            }
        };
        
        try
        {
            // Act - 连接
            var connectResult = await client.ConnectAsync();
            Assert.True(connectResult);
            
            // 等待连接稳定
            await Task.Delay(500);
            
            // Act - 发送二进制消息
            var binaryData = Encoding.UTF8.GetBytes("Binary test data");
            var sendResult = await client.SendBinaryAsync(binaryData);
            Assert.True(sendResult);
            
            // 等待消息被回显
            await Task.Delay(1000);
            
            // Act - 关闭连接
            await client.CloseAsync();
            
            // Assert
            Assert.True(binaryReceived);
        }
        finally
        {
            // 清理资源
            client.Dispose();
        }
    }

    /// <summary>
    /// 测试连接到不存在的服务器
    /// </summary>
    [Fact]
    public async Task WebSocketClient_ConnectToInvalidServer_Fails()
    {
        // Arrange
        var client = new WebSocketClient("wss://invalid-server-that-does-not-exist.example");
        var errorReceived = false;
        
        client.OnError += (sender, args) => errorReceived = true;
        
        try
        {
            // Act
            var result = await client.ConnectAsync();
            
            // Assert
            Assert.False(result);
            Assert.True(errorReceived);
        }
        finally
        {
            // 清理资源
            client.Dispose();
        }
    }

    /// <summary>
    /// 测试在未连接状态下发送消息
    /// </summary>
    [Fact]
    public async Task WebSocketClient_SendWithoutConnect_Fails()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);
        
        // Act
        var textResult = await client.SendTextAsync("Test message");
        var binaryResult = await client.SendBinaryAsync([1, 2, 3]);
        
        // Assert
        Assert.False(textResult);
        Assert.False(binaryResult);
    }

    /// <summary>
    /// 测试在未连接状态下关闭连接
    /// </summary>
    [Fact]
    public async Task WebSocketClient_CloseWithoutConnect_Fails()
    {
        // Arrange
        var client = new WebSocketClient(TestWebSocketUrl);
        
        // Act
        var result = await client.CloseAsync();
        
        // Assert
        Assert.False(result);
    }
} 
