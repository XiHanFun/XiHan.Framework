using XiHan.Framework.Serialization.Dynamic;
using XiHan.Framework.Utils.Logging;

//Examples.ProgressBarExample();

var json = "{\"code\":200,\"msg\":\"success\",\"time\":\"1761394982\",\"data\":{\"merchantNo\":\"90475836\",\"merchantSn\":\"20251025174252793021175\",\"sn\":\"0095fbae29f84de9a6d061966188bf20\",\"fee\":\"223.59\",\"status\":1}}";

var result = json.ToDynamic();
var data = result.data;
var fee = (double?)data.fee;

LogHelper.Info(result?.ToString());

Console.ReadKey();
