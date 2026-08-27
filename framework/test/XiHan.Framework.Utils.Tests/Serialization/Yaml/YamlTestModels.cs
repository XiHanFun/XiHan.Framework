// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YAML 测试用服务端配置
/// </summary>
public sealed class YamlSampleServer
{
    /// <summary>
    /// 主机名
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; }
}

/// <summary>
/// YAML 测试用应用配置，含嵌套对象
/// </summary>
public sealed class YamlSampleConfig
{
    /// <summary>
    /// 应用名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 服务端配置
    /// </summary>
    public YamlSampleServer Server { get; set; } = new();
}

/// <summary>
/// YAML 测试用带集合的配置
/// </summary>
/// <remarks>
/// 单独拆出来，避免集合成员干扰纯标量配置的往返用例。
/// </remarks>
public sealed class YamlSampleTagged
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 标签集合
    /// </summary>
    public List<string> Tags { get; set; } = [];
}
