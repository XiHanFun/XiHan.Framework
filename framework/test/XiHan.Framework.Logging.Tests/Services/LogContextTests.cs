// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Logging.Services;

namespace XiHan.Framework.Logging.Tests.Services;

/// <summary>
/// 日志上下文测试
/// </summary>
/// <remarks>
/// 上下文作用域承诺「离开作用域后恢复原状」，这是它区别于普通字典的唯一价值；
/// 恢复语义分两种：覆盖已有键要还原旧值，新增键要整个移除。两条路径都必须逐一验证。
/// </remarks>
public class LogContextTests
{
    /// <summary>
    /// 新建上下文的身份字段全为空且属性表为空
    /// </summary>
    [Fact]
    public void Defaults_HaveNoIdentityFieldsAndNoProperties()
    {
        var context = new LogContext();

        Assert.Null(context.UserId);
        Assert.Null(context.UserName);
        Assert.Null(context.TenantId);
        Assert.Null(context.RequestId);
        Assert.Null(context.TraceId);
        Assert.Null(context.SessionId);
        Assert.Null(context.IpAddress);
        Assert.Null(context.UserAgent);
        Assert.Empty(context.Properties);
    }

    /// <summary>
    /// 身份字段可独立赋值互不影响
    /// </summary>
    [Fact]
    public void IdentityFields_AreIndependentlyAssignable()
    {
        var context = new LogContext
        {
            UserId = "u1",
            UserName = "张三",
            TenantId = "t1",
            RequestId = "r1",
            TraceId = "trace1",
            SessionId = "s1",
            IpAddress = "10.0.0.1",
            UserAgent = "agent"
        };

        Assert.Equal("u1", context.UserId);
        Assert.Equal("张三", context.UserName);
        Assert.Equal("t1", context.TenantId);
        Assert.Equal("r1", context.RequestId);
        Assert.Equal("trace1", context.TraceId);
        Assert.Equal("s1", context.SessionId);
        Assert.Equal("10.0.0.1", context.IpAddress);
        Assert.Equal("agent", context.UserAgent);
    }

    /// <summary>
    /// 设置后可按原类型取回
    /// </summary>
    [Fact]
    public void SetProperty_ThenGetProperty_ReturnsTypedValue()
    {
        var context = new LogContext();

        context.SetProperty("count", 42);
        context.SetProperty("name", "abc");

        Assert.Equal(42, context.GetProperty<int>("count"));
        Assert.Equal("abc", context.GetProperty<string>("name"));
    }

    /// <summary>
    /// 同键重复设置以最后一次为准
    /// </summary>
    [Fact]
    public void SetProperty_WithSameKeyTwice_KeepsLatestValue()
    {
        var context = new LogContext();

        context.SetProperty("k", "v1");
        context.SetProperty("k", "v2");

        Assert.Equal("v2", context.GetProperty<string>("k"));
        Assert.Single(context.Properties);
    }

    /// <summary>
    /// 键不存在时返回类型默认值
    /// </summary>
    [Fact]
    public void GetProperty_WhenKeyMissing_ReturnsTypeDefault()
    {
        var context = new LogContext();

        Assert.Null(context.GetProperty<string>("missing"));
        Assert.Equal(0, context.GetProperty<int>("missing"));
    }

    /// <summary>
    /// 类型不匹配时返回类型默认值而不是抛异常
    /// </summary>
    /// <remarks>
    /// 上下文是跨模块共享的松散字典，取值方对写值方的类型没有编译期约束，
    /// 类型不符时抛异常会把日志问题升级成业务故障。
    /// </remarks>
    [Fact]
    public void GetProperty_WhenTypeMismatch_ReturnsTypeDefault()
    {
        var context = new LogContext();
        context.SetProperty("k", 1);

        Assert.Null(context.GetProperty<string>("k"));
        Assert.Equal(1, context.GetProperty<int>("k"));
    }

    /// <summary>
    /// 移除存在的键返回 true，移除不存在的键返回 false
    /// </summary>
    [Fact]
    public void RemoveProperty_ReportsWhetherKeyExisted()
    {
        var context = new LogContext();
        context.SetProperty("k", "v");

        Assert.True(context.RemoveProperty("k"));
        Assert.False(context.RemoveProperty("k"));
        Assert.Empty(context.Properties);
    }

    /// <summary>
    /// 属性表对外暴露的是快照，改快照不会回写上下文
    /// </summary>
    [Fact]
    public void Properties_ReturnsSnapshotThatDoesNotWriteBack()
    {
        var context = new LogContext();
        context.SetProperty("k", "v");

        var first = context.Properties;
        first["injected"] = "x";

        Assert.Null(context.GetProperty<string>("injected"));
        Assert.NotSame(first, context.Properties);
        Assert.Single(context.Properties);
    }

    /// <summary>
    /// 清空同时重置属性表与全部身份字段
    /// </summary>
    [Fact]
    public void Clear_ResetsPropertiesAndIdentityFields()
    {
        var context = new LogContext
        {
            UserId = "u1",
            UserName = "张三",
            TenantId = "t1",
            RequestId = "r1",
            TraceId = "trace1",
            SessionId = "s1",
            IpAddress = "10.0.0.1",
            UserAgent = "agent"
        };
        context.SetProperty("k", "v");

        context.Clear();

        Assert.Empty(context.Properties);
        Assert.Null(context.UserId);
        Assert.Null(context.UserName);
        Assert.Null(context.TenantId);
        Assert.Null(context.RequestId);
        Assert.Null(context.TraceId);
        Assert.Null(context.SessionId);
        Assert.Null(context.IpAddress);
        Assert.Null(context.UserAgent);
    }

    /// <summary>
    /// 作用域内新增的键在释放后被移除
    /// </summary>
    [Fact]
    public void CreateScope_WithNewKey_RemovesKeyOnDispose()
    {
        var context = new LogContext();

        using (context.CreateScope("scoped", "v"))
        {
            Assert.Equal("v", context.GetProperty<string>("scoped"));
        }

        Assert.Null(context.GetProperty<string>("scoped"));
        Assert.Empty(context.Properties);
    }

    /// <summary>
    /// 作用域覆盖已有键时在释放后还原旧值
    /// </summary>
    [Fact]
    public void CreateScope_WithExistingKey_RestoresOriginalValueOnDispose()
    {
        var context = new LogContext();
        context.SetProperty("k", "original");

        using (context.CreateScope("k", "override"))
        {
            Assert.Equal("override", context.GetProperty<string>("k"));
        }

        Assert.Equal("original", context.GetProperty<string>("k"));
    }

    /// <summary>
    /// 一次性推入多个属性，释放时整体还原
    /// </summary>
    [Fact]
    public void CreateScope_WithPropertyDictionary_AppliesAndRestoresEveryKey()
    {
        var context = new LogContext();
        context.SetProperty("kept", "original");

        using (context.CreateScope(new Dictionary<string, object>
        {
            ["kept"] = "override",
            ["added"] = "new"
        }))
        {
            Assert.Equal("override", context.GetProperty<string>("kept"));
            Assert.Equal("new", context.GetProperty<string>("added"));
        }

        Assert.Equal("original", context.GetProperty<string>("kept"));
        Assert.Null(context.GetProperty<string>("added"));
        Assert.Single(context.Properties);
    }

    /// <summary>
    /// 嵌套作用域按后进先出逐层还原
    /// </summary>
    [Fact]
    public void CreateScope_Nested_RestoresLayerByLayer()
    {
        var context = new LogContext();
        context.SetProperty("k", "level0");

        using (context.CreateScope("k", "level1"))
        {
            Assert.Equal("level1", context.GetProperty<string>("k"));

            using (context.CreateScope("k", "level2"))
            {
                Assert.Equal("level2", context.GetProperty<string>("k"));
            }

            Assert.Equal("level1", context.GetProperty<string>("k"));
        }

        Assert.Equal("level0", context.GetProperty<string>("k"));
    }

    /// <summary>
    /// 作用域重复释放不会再次改写上下文
    /// </summary>
    [Fact]
    public void CreateScope_DisposedTwice_DoesNotTouchContextAgain()
    {
        var context = new LogContext();
        var scope = context.CreateScope("k", "scoped");

        scope.Dispose();
        context.SetProperty("k", "after");
        scope.Dispose();

        Assert.Equal("after", context.GetProperty<string>("k"));
    }

    /// <summary>
    /// 多线程并发写入不丢键
    /// </summary>
    /// <remarks>
    /// 上下文以并发字典为底座，请求级中间件与业务线程会同时往里写，
    /// 这里用固定的键集合验证「不丢键、不抛异常」，不涉及任何真实等待。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void SetProperty_FromMultipleThreads_KeepsEveryKey()
    {
        var context = new LogContext();

        Parallel.For(0, 500, index => context.SetProperty($"k{index}", index));

        var snapshot = context.Properties;
        Assert.Equal(500, snapshot.Count);
        Assert.Equal(0, snapshot["k0"]);
        Assert.Equal(499, snapshot["k499"]);
    }
}
