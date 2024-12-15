#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PreConfigureActionList
// Guid:dd6059ba-83e4-46fc-be5f-cef894d93687
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 20:47:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Options;

/// <summary>
/// 预配置泛型委托列表
/// </summary>
public class PreConfigureActionList<TOptions> : List<Action<TOptions>>
{
    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="options"></param>
    public void Configure(TOptions options)
    {
        foreach (var action in this)
        {
            action(options);
        }
    }

    /// <summary>
    /// 配置
    /// </summary>
    /// <returns></returns>
    public TOptions Configure()
    {
        var options = Activator.CreateInstance<TOptions>();
        Configure(options);
        return options;
    }
}
