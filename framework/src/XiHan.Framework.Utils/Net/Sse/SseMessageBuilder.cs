#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SseMessageBuilder
// Guid:e2a47b9c-5f38-42d1-b7c9-83f24096d1ae
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 09:16:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// SSE消息构建器，用于从接收的数据流中构建SSE消息
/// </summary>
internal class SseMessageBuilder
{
    private readonly StringBuilder _dataBuilder = new();
    private string? _id;
    private string _event = "message";
    private int? _retry;

    /// <summary>
    /// 添加数据行
    /// </summary>
    /// <param name="data">数据内容</param>
    public void AppendData(string data)
    {
        if (_dataBuilder.Length > 0)
        {
            _dataBuilder.AppendLine();
        }
        _dataBuilder.Append(data);
    }

    /// <summary>
    /// 设置事件类型
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public void SetEvent(string eventType)
    {
        _event = eventType;
    }

    /// <summary>
    /// 设置事件ID
    /// </summary>
    /// <param name="id">事件ID</param>
    public void SetId(string id)
    {
        _id = id;
    }

    /// <summary>
    /// 设置重试间隔
    /// </summary>
    /// <param name="retry">重试间隔(毫秒)</param>
    public void SetRetry(int retry)
    {
        _retry = retry;
    }

    /// <summary>
    /// 构建SSE消息
    /// </summary>
    /// <returns>构建的SSE消息</returns>
    public SseMessage Build()
    {
        return new SseMessage
        {
            Id = _id,
            Event = _event,
            Data = _dataBuilder.ToString(),
            Retry = _retry
        };
    }

    /// <summary>
    /// 重置构建器状态
    /// </summary>
    public void Reset()
    {
        _dataBuilder.Clear();
        _id = null;
        _event = "message";
        _retry = null;
    }
}
