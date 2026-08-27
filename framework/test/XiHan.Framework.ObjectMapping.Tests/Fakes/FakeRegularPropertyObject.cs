// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Extensions.Data;

namespace XiHan.Framework.ObjectMapping.Tests.Fakes;

/// <summary>
/// 同时具备常规属性与额外属性的对象替身
/// </summary>
/// <remarks>
/// 三个常规属性分别覆盖 SetExtraPropertiesToRegularProperties 的三种分支：
/// 公有 setter、非公有 setter（GetSetMethod(true) 仍可取到）、无 setter。
/// </remarks>
public class FakeRegularPropertyObject : IHasExtraProperties
{
    /// <summary>
    /// 额外属性字典
    /// </summary>
    public ExtraPropertyDictionary ExtraProperties { get; } = new();

    /// <summary>
    /// 公有 setter，应被回填
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 私有 setter，应被回填
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// 只读属性，无 setter，不应被回填
    /// </summary>
    public string ReadOnlyText => "只读";
}
