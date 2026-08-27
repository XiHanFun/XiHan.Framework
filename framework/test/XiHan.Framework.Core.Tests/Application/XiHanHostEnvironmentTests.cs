// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 曦寒宿主环境测试
/// </summary>
/// <remarks>
/// 环境名必须是「接口上可写」的：应用装配末尾会通过 <see cref="IXiHanHostEnvironment"/> 而不是具体类
/// 把未设置的环境兜底成 Production，如果接口只暴露 get，那段兜底逻辑根本编译不过。
/// 因此这里把可写性锁在接口层面，而不是只测具体类的属性。
/// </remarks>
public class XiHanHostEnvironmentTests
{
    /// <summary>
    /// 新实例的环境名为空，等待宿主或框架兜底填充
    /// </summary>
    [Fact]
    public void EnvironmentName_Default_IsNull()
    {
        var environment = new XiHanHostEnvironment();

        Assert.Null(environment.EnvironmentName);
    }

    /// <summary>
    /// 环境名可通过接口引用写入并读回
    /// </summary>
    [Fact]
    public void EnvironmentName_IsWritableThroughInterface()
    {
        IXiHanHostEnvironment environment = new XiHanHostEnvironment
        {
            EnvironmentName = "Development"
        };

        Assert.Equal("Development", environment.EnvironmentName);

        environment.EnvironmentName = "Production";

        Assert.Equal("Production", environment.EnvironmentName);
    }

    /// <summary>
    /// 接口上的环境名同时具备读写访问器
    /// </summary>
    [Fact]
    public void Interface_ExposesReadWriteEnvironmentName()
    {
        var property = typeof(IXiHanHostEnvironment).GetProperty(nameof(IXiHanHostEnvironment.EnvironmentName));

        Assert.NotNull(property);
        Assert.NotNull(property!.GetMethod);
        Assert.NotNull(property.SetMethod);
        Assert.Equal(typeof(string), property.PropertyType);
    }

    /// <summary>
    /// 具体类落在宿主环境契约上
    /// </summary>
    [Fact]
    public void Type_ImplementsHostEnvironmentContract()
    {
        Assert.IsAssignableFrom<IXiHanHostEnvironment>(new XiHanHostEnvironment());
    }
}
