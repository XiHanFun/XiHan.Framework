#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCorsOptions
// Guid:a7b3c4d5-6e8f-4a9b-8c1d-2e3f4a5b6c7d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/06 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Cors;

/// <summary>
/// 跨域资源共享(CORS)配置选项
/// </summary>
public class XiHanCorsOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Api:Cors";

    /// <summary>
    /// 是否启用 CORS
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 允许的来源地址列表
    /// </summary>
    /// <remarks>
    /// 当 <see cref="AllowAnyOrigin"/> 为 true 时此配置被忽略
    /// </remarks>
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    /// 是否允许任意来源
    /// </summary>
    /// <remarks>
    /// 注意：当 <see cref="AllowCredentials"/> 为 true 时，不能使用 AllowAnyOrigin
    /// </remarks>
    public bool AllowAnyOrigin { get; set; } = false;

    /// <summary>
    /// 允许的 HTTP 方法列表
    /// </summary>
    /// <remarks>
    /// 当 <see cref="AllowAnyMethod"/> 为 true 时此配置被忽略。
    /// 为空且 AllowAnyMethod 为 false 时，默认允许所有方法
    /// </remarks>
    public List<string> AllowedMethods { get; set; } = [];

    /// <summary>
    /// 是否允许任意 HTTP 方法
    /// </summary>
    public bool AllowAnyMethod { get; set; } = true;

    /// <summary>
    /// 允许的请求头列表
    /// </summary>
    /// <remarks>
    /// 当 <see cref="AllowAnyHeader"/> 为 true 时此配置被忽略。
    /// 为空且 AllowAnyHeader 为 false 时，默认允许所有头
    /// </remarks>
    public List<string> AllowedHeaders { get; set; } = [];

    /// <summary>
    /// 是否允许任意请求头
    /// </summary>
    public bool AllowAnyHeader { get; set; } = true;

    /// <summary>
    /// 是否允许携带凭据（Cookie、Authorization 等）
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// 暴露给客户端的响应头列表
    /// </summary>
    public List<string> ExposedHeaders { get; set; } = [];

    /// <summary>
    /// 预检请求缓存时间（秒），0 表示不设置
    /// </summary>
    public int PreflightMaxAgeSeconds { get; set; } = 0;
}
