// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.SearchEngines.Elasticsearch;
using XiHan.Framework.SearchEngines.Elasticsearch.Options;
using XiHan.Framework.SearchEngines.InMemory;

namespace XiHan.Framework.SearchEngines.Tests;

/// <summary>
/// 进程内实现的契约测试
/// </summary>
public class InMemorySearchEngineContractTests : SearchEngineContractTestsBase
{
    /// <summary>
    /// 创建被测引擎
    /// </summary>
    /// <returns>进程内引擎</returns>
    protected override Task<ISearchEngine> CreateEngineAsync()
    {
        return Task.FromResult<ISearchEngine>(new InMemorySearchEngine());
    }
}

/// <summary>
/// Elasticsearch 实现的契约测试
/// </summary>
/// <remarks>
/// 与进程内实现共用同一套断言，用于证明抽象没有偏向任一实现。
/// 需要一个可访问的 Elasticsearch；地址取自环境变量 <c>XIHAN_TEST_ELASTICSEARCH_URI</c>，
/// 缺省为 <c>http://localhost:9200</c>。不可达时整类跳过而非失败，以免无 Elasticsearch 的环境无法构建。
/// </remarks>
[Collection("Elasticsearch")]
public class ElasticsearchSearchEngineContractTests : SearchEngineContractTestsBase
{
    private static readonly string Uri =
        Environment.GetEnvironmentVariable("XIHAN_TEST_ELASTICSEARCH_URI") ?? "http://localhost:9200";

    private static readonly Lazy<bool> Reachable = new(ProbeReachable);

    /// <summary>
    /// 创建被测引擎，Elasticsearch 不可达时跳过用例
    /// </summary>
    /// <returns>Elasticsearch 引擎</returns>
    protected override Task<ISearchEngine> CreateEngineAsync()
    {
        Assert.SkipUnless(Reachable.Value, $"Elasticsearch 不可达（{Uri}），跳过该实现的契约测试。");

        var options = Options.Create(new ElasticsearchOptions
        {
            Uri = Uri,
            NumberOfReplicas = 0
        });

        return Task.FromResult<ISearchEngine>(new ElasticsearchSearchEngine(options));
    }

    /// <summary>
    /// 探测 Elasticsearch 是否可达
    /// </summary>
    /// <returns>是否可达</returns>
    private static bool ProbeReachable()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            return client.GetAsync(Uri).GetAwaiter().GetResult().IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>
/// Elasticsearch 测试集合，共享同一集群的用例串行执行
/// </summary>
[CollectionDefinition("Elasticsearch", DisableParallelization = true)]
public class ElasticsearchTestCollection;
