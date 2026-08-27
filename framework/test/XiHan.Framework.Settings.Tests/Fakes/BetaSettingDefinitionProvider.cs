// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 定义 Fake.Beta 的设置定义提供者
/// </summary>
public sealed class BetaSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <summary>
    /// 该提供者定义的设置名称
    /// </summary>
    public const string SettingName = "Fake.Beta";

    /// <summary>
    /// 该提供者定义的默认值
    /// </summary>
    public const string SettingDefaultValue = "beta-default";

    /// <summary>
    /// 定义设置
    /// </summary>
    /// <param name="context">设置定义上下文</param>
    public void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(SettingName, SettingDefaultValue, group: "OtherGroup"));
    }
}
