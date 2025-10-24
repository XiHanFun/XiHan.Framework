#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiVersionAttribute
// Guid:o5p6q7r8-s9t0-4u1v-2w3x-4y5z6a7b8c9d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Versioning;

/// <summary>
/// API 版本特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ApiVersionAttribute : Attribute
{
    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 是否已弃用
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="version">版本号</param>
    public ApiVersionAttribute(string version)
    {
        Version = version;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="majorVersion">主版本号</param>
    /// <param name="minorVersion">次版本号</param>
    public ApiVersionAttribute(int majorVersion, int minorVersion = 0)
    {
        Version = $"{majorVersion}.{minorVersion}";
    }
}

/// <summary>
/// 映射到 API 版本特性
/// 将方法映射到指定的 API 版本
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MapToApiVersionAttribute : Attribute
{
    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="version">版本号</param>
    public MapToApiVersionAttribute(string version)
    {
        Version = version;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="majorVersion">主版本号</param>
    /// <param name="minorVersion">次版本号</param>
    public MapToApiVersionAttribute(int majorVersion, int minorVersion = 0)
    {
        Version = $"{majorVersion}.{minorVersion}";
    }
}

