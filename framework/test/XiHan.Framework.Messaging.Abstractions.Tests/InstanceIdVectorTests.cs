// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 回放 <c>instance-ids.json</c>：消息子系统实例标识的文法
/// </summary>
/// <remarks>
/// <c>messaging/instance-ids.json</c>（RFC-0009「订阅方式」第 6 条，ADR-0085、ADR-0277）。经三个入口回放：内部的 <see cref="MessagingOptions.IsValidInstanceId"/>、<see cref="MessagingOptions.InstanceId"/> 的赋值，以及从配置读取（配置了不合文法的值在装配时失败）
/// </remarks>
public sealed class InstanceIdVectorTests
{
    /// <summary>
    /// 实例标识的配置键
    /// </summary>
    private const string ConfigurationKey = MessagingOptions.ConfigurationSection + ":InstanceId";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly InstanceIdVectors _vectors = ConformanceVectors.Load<InstanceIdVectors>("instance-ids.json");

    [Fact]
    public void InstanceId_AcceptsTheValidIds()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var id in _vectors.Valid)
        {
            Assert.True(MessagingOptions.IsValidInstanceId(id), id);
            Assert.Equal(id, new MessagingOptions { InstanceId = id }.InstanceId);
            Assert.Equal(id, MessagingOptions.FromConfiguration(Configuration(id)).InstanceId);
        }
    }

    [Fact]
    public void InstanceId_RejectsTheInvalidIds()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var id in _vectors.Invalid)
        {
            Assert.False(MessagingOptions.IsValidInstanceId(id), id);
            Assert.Throws<ArgumentException>(() => new MessagingOptions { InstanceId = id });
            Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Configuration(id)));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, id => id.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, id => id.Length == _vectors.MaxLength + 1 && id.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'));
    }

    /// <summary>
    /// 只配了实例标识的配置
    /// </summary>
    /// <param name="instanceId">实例标识，原样写进配置</param>
    private static IConfiguration Configuration(string instanceId)
        => new ConfigurationBuilder().AddInMemoryCollection([KeyValuePair.Create<string, string?>(ConfigurationKey, instanceId)]).Build();

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">实例标识的字符数上限</param>
    /// <param name="Valid">合文法的实例标识</param>
    /// <param name="Invalid">不合文法的实例标识</param>
    private sealed record InstanceIdVectors(int MaxLength, string[] Valid, string[] Invalid);
}
