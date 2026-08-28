// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Reflection;
using XiHan.Framework.Bot.Sms.Enums;

namespace XiHan.Framework.Bot.Sms.Tests.Enums;

/// <summary>
/// <see cref="SmsProviderType"/> 短信服务商枚举测试
/// </summary>
/// <remarks>
/// 该枚举值会被 <c>SmsChannelConfig</c> 持久化到数据库、并参与网关解析器的配置指纹拼接，
/// 数值一旦漂移会导致存量配置解析到错误服务商，因此这里把数值和成员集合都锁死。
/// </remarks>
public class SmsProviderTypeTests
{
    /// <summary>
    /// 阿里云固定为 0，腾讯云固定为 1
    /// </summary>
    /// <param name="provider">服务商枚举</param>
    /// <param name="expected">期望的底层数值</param>
    [Theory]
    [InlineData(SmsProviderType.Aliyun, 0)]
    [InlineData(SmsProviderType.TencentCloud, 1)]
    public void UnderlyingValue_IsPinned(SmsProviderType provider, int expected)
    {
        Assert.Equal(expected, (int)provider);
    }

    /// <summary>
    /// 枚举成员集合恰为阿里云与腾讯云两项
    /// </summary>
    [Fact]
    public void Members_AreExactlyAliyunAndTencentCloud()
    {
        var values = Enum.GetValues<SmsProviderType>();

        Assert.Equal(2, values.Length);
        Assert.Contains(SmsProviderType.Aliyun, values);
        Assert.Contains(SmsProviderType.TencentCloud, values);
    }

    /// <summary>
    /// 枚举默认值为阿里云，与 SmsChannelConfig.Provider 的默认值一致
    /// </summary>
    [Fact]
    public void Default_IsAliyun()
    {
        Assert.Equal(SmsProviderType.Aliyun, default(SmsProviderType));
    }

    /// <summary>
    /// 每个成员都带中文 Description 特性，供前端下拉与日志展示
    /// </summary>
    /// <param name="provider">服务商枚举</param>
    /// <param name="expected">期望的中文描述</param>
    [Theory]
    [InlineData(SmsProviderType.Aliyun, "阿里云")]
    [InlineData(SmsProviderType.TencentCloud, "腾讯云")]
    public void Description_IsChineseDisplayName(SmsProviderType provider, string expected)
    {
        var field = typeof(SmsProviderType).GetField(provider.ToString());

        Assert.NotNull(field);
        var description = field!.GetCustomAttribute<DescriptionAttribute>();
        Assert.NotNull(description);
        Assert.Equal(expected, description!.Description);
    }

    /// <summary>
    /// 未定义的数值不属于该枚举，解析器据此走「不支持的服务商」分支
    /// </summary>
    [Fact]
    public void IsDefined_ForUnknownValue_IsFalse()
    {
        Assert.False(Enum.IsDefined((SmsProviderType)99));
        Assert.True(Enum.IsDefined(SmsProviderType.Aliyun));
    }
}
