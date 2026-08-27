// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Hubs;
using XiHan.Framework.Web.RealTime.Services;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// <see cref="XiHanHub"/> 的最小具体子类
/// </summary>
/// <remarks>
/// 基类是抽象类，无法直接实例化；本类不添加任何行为，用来验证基类模板方法本身的语义。
/// </remarks>
public sealed class TestXiHanHub : XiHanHub
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionManager">连接管理器</param>
    public TestXiHanHub(IConnectionManager connectionManager)
        : base(connectionManager)
    {
    }
}
