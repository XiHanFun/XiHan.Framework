// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 定义 Fake.Alpha 的设置定义提供者
/// </summary>
/// <remarks>
/// 必须是 public 且带公共无参构造，定义管理器会用 ActivatorUtilities 反射实例化它。
/// </remarks>
public sealed class AlphaSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <summary>
    /// 该提供者定义的设置名称
    /// </summary>
    public const string SettingName = "Fake.Alpha";

    /// <summary>
    /// 该提供者定义的默认值
    /// </summary>
    public const string SettingDefaultValue = "alpha-default";

    /// <summary>
    /// 该提供者定义的分组
    /// </summary>
    public const string SettingGroup = "FakeGroup";

    /// <summary>
    /// 定义设置
    /// </summary>
    /// <param name="context">设置定义上下文</param>
    public void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(SettingName, SettingDefaultValue, group: SettingGroup));
    }
}
