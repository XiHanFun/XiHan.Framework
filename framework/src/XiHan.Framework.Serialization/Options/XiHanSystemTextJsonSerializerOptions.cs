#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSystemTextJsonSerializerOptions
// Guid:d675cdb1-7b76-4751-a60e-ff5773255376
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2025/3/12 20:53:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;

namespace XiHan.Framework.Serialization.Options;

/// <summary>
/// 曦寒System.Text.Json序列化器选项
/// </summary>
public class XiHanSystemTextJsonSerializerOptions
{
    /// <summary>
    /// System.Text.Json序列化器选项
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanSystemTextJsonSerializerOptions()
    {
        JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
}
