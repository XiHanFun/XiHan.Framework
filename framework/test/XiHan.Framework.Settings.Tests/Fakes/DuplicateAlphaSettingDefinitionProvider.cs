// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 与 <see cref="AlphaSettingDefinitionProvider"/> 定义同名设置的提供者
/// </summary>
/// <remarks>
/// 专门用来触发定义汇总阶段的重复名冲突。
/// </remarks>
public sealed class DuplicateAlphaSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <summary>
    /// 定义设置
    /// </summary>
    /// <param name="context">设置定义上下文</param>
    public void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(AlphaSettingDefinitionProvider.SettingName, "duplicate-default"));
    }
}
