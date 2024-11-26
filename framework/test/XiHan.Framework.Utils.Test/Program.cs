using XiHan.Framework.Utils.Security.Cryptography;

Console.WriteLine("Hello, Test!");

var ss = "{\"amount\":\"500\",\"merchantOrderNo\":\"20241118181906524010001\",\"customerName\":\"\",\"customerEmail\":\"\",\"customerPhone\":\"\",\"notifyUrl\":\"https://api.tkusdtapi.com/api/greenorder\"}";
var aesKey = "NjD/j53SSqnnNd3v";
var aesIv = "W7QzK1tQK9in04QY";
var sign = AesHelper.Encrypt(ss, aesKey, aesIv);
Console.WriteLine(sign);

Console.ReadKey();