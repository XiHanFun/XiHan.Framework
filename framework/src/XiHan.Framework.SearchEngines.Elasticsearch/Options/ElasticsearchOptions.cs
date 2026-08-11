// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Elasticsearch.Options;

/// <summary>
/// Elasticsearch 配置
/// </summary>
public class ElasticsearchOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:SearchEngines:Elasticsearch";

    /// <summary>
    /// 节点地址
    /// </summary>
    public string Uri { get; set; } = "http://localhost:9200";

    /// <summary>
    /// 用户名，与密码同时提供时启用基础认证
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// API 密钥，优先于用户名密码
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 索引名前缀，用于同一集群区分环境或租户
    /// </summary>
    public string IndexPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 请求超时秒数
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否跳过服务端证书校验
    /// </summary>
    /// <remarks>
    /// 仅供本地自签名证书的开发环境使用，生产环境开启等同于放弃传输层身份校验。
    /// </remarks>
    public bool AllowUntrustedCertificate { get; set; }

    /// <summary>
    /// 创建索引时的分片数
    /// </summary>
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// 创建索引时的副本数
    /// </summary>
    public int NumberOfReplicas { get; set; } = 1;
}
