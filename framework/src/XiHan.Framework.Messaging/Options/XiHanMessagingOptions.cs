// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Messaging.Options;

/// <summary>
/// 消息模块配置
/// </summary>
public class XiHanMessagingOptions
{
    /// <summary>
    /// 是否在单个接收人失败后继续发送
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// 找不到发送器时是否抛出异常
    /// </summary>
    public bool ThrowWhenNoSender { get; set; } = false;
}
