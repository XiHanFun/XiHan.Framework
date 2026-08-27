// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Localization.Abstractions.Tests.Fakes;

/// <summary>
/// 手写的"带名称与可本地化显示名"对象替身
/// </summary>
public sealed class FakeNamedDisplayObject : IHasNameWithLocalizableDisplayName
{
    /// <summary>
    /// 初始化对象替身
    /// </summary>
    /// <param name="name">标识名称</param>
    /// <param name="displayName">可本地化显示名，允许为空</param>
    public FakeNamedDisplayObject(string name, ILocalizableString? displayName = null)
    {
        Name = name;
        DisplayName = displayName;
    }

    /// <summary>
    /// 标识名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 可本地化显示名
    /// </summary>
    public ILocalizableString? DisplayName { get; }
}
