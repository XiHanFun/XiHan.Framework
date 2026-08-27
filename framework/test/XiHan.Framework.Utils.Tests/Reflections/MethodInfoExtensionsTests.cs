// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Tests.Reflections;

/// <summary>
/// 方法信息扩展方法测试
/// </summary>
public class MethodInfoExtensionsTests
{
    /// <summary>
    /// 返回 Task 与 Task&lt;T&gt; 的方法判为异步
    /// </summary>
    [Theory]
    [InlineData(nameof(Sample.RunAsync))]
    [InlineData(nameof(Sample.ComputeAsync))]
    public void IsAsync_WhenReturnsTask_ReturnsTrue(string methodName)
    {
        Assert.True(GetMethod<Sample>(methodName).IsAsync());
    }

    /// <summary>
    /// 同步方法与返回 ValueTask 的方法都不判为异步
    /// </summary>
    [Theory]
    [InlineData(nameof(Sample.RunSync))]
    [InlineData(nameof(Sample.Compute))]
    [InlineData(nameof(Sample.RunValueTask))]
    public void IsAsync_WhenNotReturningTask_ReturnsFalse(string methodName)
    {
        Assert.False(GetMethod<Sample>(methodName).IsAsync());
    }

    /// <summary>
    /// 派生类重写的方法判为重写
    /// </summary>
    [Fact]
    public void IsOverridden_WhenMethodIsOverride_ReturnsTrue()
    {
        Assert.True(GetMethod<Derived>(nameof(Base.Describe)).IsOverridden());
    }

    /// <summary>
    /// 基类的虚方法自身不算重写
    /// </summary>
    [Fact]
    public void IsOverridden_OnBaseVirtualMethod_ReturnsFalse()
    {
        Assert.False(GetMethod<Base>(nameof(Base.Describe)).IsOverridden());
    }

    /// <summary>
    /// 普通非虚方法不算重写
    /// </summary>
    [Fact]
    public void IsOverridden_OnPlainMethod_ReturnsFalse()
    {
        Assert.False(GetMethod<Sample>(nameof(Sample.RunSync)).IsOverridden());
    }

    /// <summary>
    /// 派生类未重写时取到的是基类方法，同样不算重写
    /// </summary>
    [Fact]
    public void IsOverridden_WhenDerivedDoesNotOverride_ReturnsFalse()
    {
        Assert.False(GetMethod<Derived>(nameof(Base.Keep)).IsOverridden());
    }

    /// <summary>
    /// 取指定类型上的公有实例方法
    /// </summary>
    private static MethodInfo GetMethod<T>(string name)
    {
        return typeof(T).GetMethod(name)!;
    }

    /// <summary>
    /// 测试用类型：覆盖同步、异步与 ValueTask 返回值
    /// </summary>
    private sealed class Sample
    {
        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

        public Task<int> ComputeAsync()
        {
            return Task.FromResult(1);
        }

        public ValueTask RunValueTask()
        {
            return ValueTask.CompletedTask;
        }

        public void RunSync()
        {
        }

        public int Compute()
        {
            return 1;
        }
    }

    /// <summary>
    /// 测试用基类
    /// </summary>
    private class Base
    {
        public virtual string Describe()
        {
            return nameof(Base);
        }

        public virtual string Keep()
        {
            return nameof(Base);
        }
    }

    /// <summary>
    /// 测试用派生类，只重写其中一个方法
    /// </summary>
    private sealed class Derived : Base
    {
        public override string Describe()
        {
            return nameof(Derived);
        }
    }
}
