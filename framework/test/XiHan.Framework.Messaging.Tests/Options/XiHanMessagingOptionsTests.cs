// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Messaging.Options;

namespace XiHan.Framework.Messaging.Tests;

/// <summary>
/// 消息模块配置测试
/// </summary>
/// <remarks>
/// 两个开关都是「宽容优先」的默认取向：单个接收人失败继续发、找不到发送器不炸。
/// 默认值一旦反转，业务侧不改代码就会从「拿到失败结果」变成「被抛异常打断」，属破坏性变更，这里锁死。
/// </remarks>
public class XiHanMessagingOptionsTests
{
    /// <summary>
    /// 默认配置对失败保持宽容
    /// </summary>
    [Fact]
    public void Constructor_Default_IsLenientOnFailure()
    {
        var options = new XiHanMessagingOptions();

        Assert.True(options.ContinueOnError);
        Assert.False(options.ThrowWhenNoSender);
    }

    /// <summary>
    /// 两个开关互相独立可写
    /// </summary>
    [Fact]
    public void Properties_AreIndependentlyMutable()
    {
        var options = new XiHanMessagingOptions
        {
            ContinueOnError = false
        };

        Assert.False(options.ContinueOnError);
        Assert.False(options.ThrowWhenNoSender);

        options.ThrowWhenNoSender = true;

        Assert.False(options.ContinueOnError);
        Assert.True(options.ThrowWhenNoSender);
    }
}
