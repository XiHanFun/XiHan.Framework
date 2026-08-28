// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Extensions.Data;

namespace XiHan.Framework.ObjectMapping.Tests.Fakes;

/// <summary>
/// 手写的可扩展对象替身
/// </summary>
/// <remarks>
/// 只实现 <see cref="IHasExtraProperties"/> 要求的字典，不掺杂业务字段。
/// <see cref="ObjectExtensionManager"/> 以「运行时类型」为键做隔离，因此各测试类会从本类型派生出
/// 各自专属的空子类，避免共享单例被相互污染。
/// </remarks>
public class FakeExtensibleObject : IHasExtraProperties
{
    /// <summary>
    /// 额外属性字典
    /// </summary>
    public ExtraPropertyDictionary ExtraProperties { get; } = [];
}
