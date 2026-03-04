using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.Dtos;

/// <summary>
/// 机器人通用返回结果
/// </summary>
public class BotResult
{
    /// <summary>
    /// 业务码（默认 200 表示成功，序列化到 JSON 为 int）
    /// </summary>
    public BotResultCodes Code { get; set; } = BotResultCodes.Success;

    /// <summary>
    /// 响应信息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 数据集合
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Code == BotResultCodes.Success;

    /// <summary>
    /// 响应成功，返回通用数据 200
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static BotResult Success(object? data)
    {
        return new BotResult
        {
            Code = BotResultCodes.Success,
            Message = BotResultCodes.Success.GetDescription(),
            Data = data
        };
    }

    /// <summary>
    /// 响应失败，访问出错 400
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public static BotResult BadRequest(string? errorMessage = null)
    {
        return new BotResult
        {
            Code = BotResultCodes.BadRequest,
            Message = BotResultCodes.BadRequest.GetDescription(),
            Data = errorMessage
        };
    }

    /// <summary>
    /// 响应失败，服务器内部错误 500
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public static BotResult Failed(string? errorMessage = null)
    {
        return new BotResult
        {
            Code = BotResultCodes.Failed,
            Message = BotResultCodes.Failed.GetDescription(),
            Data = errorMessage
        };
    }
}
