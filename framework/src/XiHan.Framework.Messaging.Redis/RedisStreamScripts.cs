// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Messaging.Redis;

/// <summary>
/// 流与消费组上需要原子完成的 Lua 脚本
/// </summary>
/// <remarks>
/// 每段脚本只操作 <c>KEYS[1]</c> 这一条流，Redis 集群里也落在一个槽上。脚本按 RESP2 取回复：<c>XINFO</c> 的每一项是字段名与值交替的数组，空值是 <c>false</c>
/// </remarks>
internal static class RedisStreamScripts
{
    /// <summary>
    /// 读 <c>INFO memory</c>
    /// </summary>
    /// <remarks>
    /// StackExchange.Redis 只在管理模式下放行 <c>INFO</c> 命令，连接时检查淘汰策略经这段脚本读
    /// </remarks>
    public const string MemoryInfo = "return redis.call('INFO', 'memory')";

    /// <summary>
    /// 建消费组（不存在时连同流一起建）并建本实例的消费者
    /// </summary>
    /// <remarks>
    /// <c>KEYS[1]</c> 流，<c>ARGV[1]</c> 组名，<c>ARGV[2]</c> 消费者名。组从 <c>$</c> 开始，订阅前的条目不投递；组已存在（<c>BUSYGROUP</c>）时只建消费者。组与消费者在同一段脚本里建出，别的实例维护时不会看到一个还没有消费者的新组而把它当成遗留的广播组删掉
    /// </remarks>
    public const string EnsureGroup = """
        local created = redis.pcall('XGROUP', 'CREATE', KEYS[1], ARGV[1], '$', 'MKSTREAM')
        if type(created) == 'table' and created.err ~= nil and string.find(created.err, 'BUSYGROUP', 1, true) == nil then
          return redis.error_reply(created.err)
        end
        redis.call('XGROUP', 'CREATECONSUMER', KEYS[1], ARGV[1], ARGV[2])
        return 1
        """;

    /// <summary>
    /// 维护一条流：清理遗留的广播组与空闲的消费者，再去掉全部组都确认过的条目
    /// </summary>
    /// <remarks>
    /// <c>KEYS[1]</c> 流；<c>ARGV[1]</c> 广播组名的前缀 <c>&lt;GroupName&gt;@</c>，<c>ARGV[2]</c> 本循环的组，<c>ARGV[3]</c> 本实例的广播组，<c>ARGV[4]</c> 本实例的消费者名，<c>ARGV[5]</c> 空闲阈值（毫秒），<c>ARGV[6]</c> 为 <c>1</c> 时清理 <c>ARGV[2]</c> 里的空闲消费者（竞争组）。
    /// 广播组没有消费者、或全部消费者空闲超过阈值时删除：在线的实例每个轮询周期都读一次，空闲不会超过阈值。竞争组里空闲超过阈值且没有待确认条目的其他消费者删除：<c>XGROUP DELCONSUMER</c> 会连同待确认条目一起删掉，有待确认条目的留给回收。
    /// 本循环的组不存在（被外部删除）时不碰它，留给消费循环按 <c>NOGROUP</c> 重新建组。
    /// 整理的下限：每个组有待确认条目时取最早一条，否则取最后投递的 ID，全部组里取最小的，以 <c>XTRIM MINID ~</c> 去掉更早的条目。一个组也不剩时删除流：之后的发布按没有订阅者丢弃，新组从 <c>$</c> 开始也收不到这些条目。返回 <c>{删除的组数, 删除的消费者数, 去掉的条目数}</c>
    /// </remarks>
    public const string Maintain = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
          return {0, 0, 0}
        end
        local threshold = tonumber(ARGV[5])
        local function field(entry, name)
          for i = 1, #entry, 2 do
            if entry[i] == name then
              return entry[i + 1]
            end
          end
          return nil
        end
        local removedGroups = 0
        local ownGroupExists = false
        for _, group in ipairs(redis.call('XINFO', 'GROUPS', KEYS[1])) do
          local name = field(group, 'name')
          if name == ARGV[2] then
            ownGroupExists = true
          elseif name ~= ARGV[3] and string.sub(name, 1, #ARGV[1]) == ARGV[1] then
            local alive = false
            for _, consumer in ipairs(redis.call('XINFO', 'CONSUMERS', KEYS[1], name)) do
              if tonumber(field(consumer, 'idle')) <= threshold then
                alive = true
                break
              end
            end
            if not alive then
              redis.call('XGROUP', 'DESTROY', KEYS[1], name)
              removedGroups = removedGroups + 1
            end
          end
        end
        local removedConsumers = 0
        if ARGV[6] == '1' and ownGroupExists then
          for _, consumer in ipairs(redis.call('XINFO', 'CONSUMERS', KEYS[1], ARGV[2])) do
            local name = field(consumer, 'name')
            if name ~= ARGV[4] and tonumber(field(consumer, 'pending')) == 0 and tonumber(field(consumer, 'idle')) > threshold then
              redis.call('XGROUP', 'DELCONSUMER', KEYS[1], ARGV[2], name)
              removedConsumers = removedConsumers + 1
            end
          end
        end
        local groups = redis.call('XINFO', 'GROUPS', KEYS[1])
        if #groups == 0 then
          redis.call('DEL', KEYS[1])
          return {removedGroups, removedConsumers, 0}
        end
        local floor, floorMs, floorSeq
        for _, group in ipairs(groups) do
          local candidate = field(group, 'last-delivered-id')
          local pending = redis.call('XPENDING', KEYS[1], field(group, 'name'))
          if tonumber(pending[1]) > 0 then
            candidate = pending[2]
          end
          local ms, seq = string.match(candidate, '^(%d+)-(%d+)$')
          ms, seq = tonumber(ms), tonumber(seq)
          if floor == nil or ms < floorMs or (ms == floorMs and seq < floorSeq) then
            floor, floorMs, floorSeq = candidate, ms, seq
          end
        end
        local trimmed = redis.call('XTRIM', KEYS[1], 'MINID', '~', floor)
        return {removedGroups, removedConsumers, trimmed}
        """;

    /// <summary>
    /// 停止时删除本实例在竞争组里的消费者
    /// </summary>
    /// <remarks>
    /// <c>KEYS[1]</c> 流，<c>ARGV[1]</c> 组名，<c>ARGV[2]</c> 消费者名。还有待确认条目时不删，留给别的实例回收；流或组已不存在时什么也不做。返回删除的消费者数
    /// </remarks>
    public const string ReleaseConsumer = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
          return 0
        end
        for _, group in ipairs(redis.call('XINFO', 'GROUPS', KEYS[1])) do
          for i = 1, #group, 2 do
            if group[i] == 'name' and group[i + 1] == ARGV[1] then
              if #redis.call('XPENDING', KEYS[1], ARGV[1], '-', '+', 1, ARGV[2]) > 0 then
                return 0
              end
              return redis.call('XGROUP', 'DELCONSUMER', KEYS[1], ARGV[1], ARGV[2])
            end
          end
        end
        return 0
        """;

    /// <summary>
    /// 停止时删除本实例的广播组，流上一个组也不剩时连流一起删除
    /// </summary>
    /// <remarks>
    /// <c>KEYS[1]</c> 流，<c>ARGV[1]</c> 组名。流不存在时什么也不做。返回删除的组数
    /// </remarks>
    public const string ReleaseGroup = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
          return 0
        end
        local destroyed = redis.call('XGROUP', 'DESTROY', KEYS[1], ARGV[1])
        if #redis.call('XINFO', 'GROUPS', KEYS[1]) == 0 then
          redis.call('DEL', KEYS[1])
        end
        return destroyed
        """;
}
