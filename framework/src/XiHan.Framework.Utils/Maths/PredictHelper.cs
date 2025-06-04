#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PredictHelper
// Guid:066e3c10-c27e-4756-a682-cab7fb7b46ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 2:35:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// 数据预测辅助类
/// </summary>
/// <remarks>
/// 提供多种数据预测算法，包括线性回归、移动平均、指数平滑、趋势分析等，适用于时间序列预测、趋势分析和数据挖掘场景
/// </remarks>
public static class PredictHelper
{
    /// <summary>
    /// 使用线性回归进行预测
    /// </summary>
    /// <param name="xValues">自变量数据</param>
    /// <param name="yValues">因变量数据</param>
    /// <param name="predictX">要预测的自变量值</param>
    /// <returns>预测的因变量值</returns>
    /// <exception cref="ArgumentNullException">输入数据为空时抛出</exception>
    /// <exception cref="ArgumentException">数据长度不匹配或数据量不足时抛出</exception>
    public static double LinearRegression(double[] xValues, double[] yValues, double predictX)
    {
        ArgumentNullException.ThrowIfNull(xValues);
        ArgumentNullException.ThrowIfNull(yValues);

        if (xValues.Length != yValues.Length)
        {
            throw new ArgumentException("自变量和因变量数据长度必须相同");
        }

        if (xValues.Length < 2)
        {
            throw new ArgumentException("至少需要2个数据点进行线性回归");
        }

        var n = xValues.Length;
        var sumX = xValues.Sum();
        var sumY = yValues.Sum();
        var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
        var sumXX = xValues.Sum(x => x * x);

        // 计算线性回归系数：y = a + b*x
        var b = ((n * sumXY) - (sumX * sumY)) / ((n * sumXX) - (sumX * sumX));
        var a = (sumY - (b * sumX)) / n;

        return a + (b * predictX);
    }

    /// <summary>
    /// 使用线性回归获取回归系数
    /// </summary>
    /// <param name="xValues">自变量数据</param>
    /// <param name="yValues">因变量数据</param>
    /// <returns>回归系数 (截距, 斜率)</returns>
    public static (double intercept, double slope) GetLinearRegressionCoefficients(double[] xValues, double[] yValues)
    {
        ArgumentNullException.ThrowIfNull(xValues);
        ArgumentNullException.ThrowIfNull(yValues);

        if (xValues.Length != yValues.Length)
        {
            throw new ArgumentException("自变量和因变量数据长度必须相同");
        }

        if (xValues.Length < 2)
        {
            throw new ArgumentException("至少需要2个数据点进行线性回归");
        }

        var n = xValues.Length;
        var sumX = xValues.Sum();
        var sumY = yValues.Sum();
        var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
        var sumXX = xValues.Sum(x => x * x);

        var slope = ((n * sumXY) - (sumX * sumY)) / ((n * sumXX) - (sumX * sumX));
        var intercept = (sumY - (slope * sumX)) / n;

        return (intercept, slope);
    }

    /// <summary>
    /// 计算相关系数（衡量线性相关程度）
    /// </summary>
    /// <param name="xValues">自变量数据</param>
    /// <param name="yValues">因变量数据</param>
    /// <returns>相关系数 (-1 到 1 之间)</returns>
    public static double CalculateCorrelationCoefficient(double[] xValues, double[] yValues)
    {
        ArgumentNullException.ThrowIfNull(xValues);
        ArgumentNullException.ThrowIfNull(yValues);

        if (xValues.Length != yValues.Length)
        {
            throw new ArgumentException("自变量和因变量数据长度必须相同");
        }

        if (xValues.Length < 2)
        {
            throw new ArgumentException("至少需要2个数据点计算相关系数");
        }

        var n = xValues.Length;
        var sumX = xValues.Sum();
        var sumY = yValues.Sum();
        var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
        var sumXX = xValues.Sum(x => x * x);
        var sumYY = yValues.Sum(y => y * y);

        var numerator = (n * sumXY) - (sumX * sumY);
        var denominator = Math.Sqrt(((n * sumXX) - (sumX * sumX)) * ((n * sumYY) - (sumY * sumY)));

        return denominator == 0 ? 0 : numerator / denominator;
    }

    /// <summary>
    /// 简单移动平均预测
    /// </summary>
    /// <param name="values">历史数据</param>
    /// <param name="period">移动平均周期</param>
    /// <returns>预测值</returns>
    /// <exception cref="ArgumentNullException">输入数据为空时抛出</exception>
    /// <exception cref="ArgumentException">周期设置不合理时抛出</exception>
    public static double SimpleMovingAverage(double[] values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);

        return period <= 0
            ? throw new ArgumentException("移动平均周期必须大于0", nameof(period))
            : values.Length < period
            ? throw new ArgumentException($"数据长度必须大于等于移动平均周期 {period}", nameof(values))
            : values.TakeLast(period).Average();
    }

    /// <summary>
    /// 加权移动平均预测
    /// </summary>
    /// <param name="values">历史数据</param>
    /// <param name="weights">权重数组（权重之和应为1）</param>
    /// <returns>预测值</returns>
    /// <exception cref="ArgumentNullException">输入数据为空时抛出</exception>
    /// <exception cref="ArgumentException">数据和权重长度不匹配时抛出</exception>
    public static double WeightedMovingAverage(double[] values, double[] weights)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(weights);

        if (weights.Length > values.Length)
        {
            throw new ArgumentException("权重数组长度不能大于数据长度");
        }

        var recentValues = values.TakeLast(weights.Length).ToArray();
        return recentValues.Zip(weights, (value, weight) => value * weight).Sum();
    }

    /// <summary>
    /// 指数平滑预测
    /// </summary>
    /// <param name="values">历史数据</param>
    /// <param name="alpha">平滑系数（范围：0 &lt; alpha &lt;= 1）</param>
    /// <returns>预测值</returns>
    /// <exception cref="ArgumentNullException">输入数据为空时抛出</exception>
    /// <exception cref="ArgumentException">平滑系数不在有效范围内时抛出</exception>
    public static double ExponentialSmoothing(double[] values, double alpha)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (alpha is <= 0 or > 1)
        {
            throw new ArgumentException("平滑系数必须在 (0, 1] 范围内", nameof(alpha));
        }

        if (values.Length == 0)
        {
            throw new ArgumentException("数据不能为空", nameof(values));
        }

        if (values.Length == 1)
        {
            return values[0];
        }

        var smoothed = values[0];
        for (var i = 1; i < values.Length; i++)
        {
            smoothed = (alpha * values[i]) + ((1 - alpha) * smoothed);
        }

        return smoothed;
    }

    /// <summary>
    /// 双重指数平滑预测（霍尔特法）
    /// </summary>
    /// <param name="values">历史数据</param>
    /// <param name="alpha">水平平滑系数</param>
    /// <param name="beta">趋势平滑系数</param>
    /// <param name="periodsAhead">预测未来的周期数</param>
    /// <returns>预测值</returns>
    public static double DoubleExponentialSmoothing(double[] values, double alpha, double beta, int periodsAhead = 1)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (alpha is <= 0 or > 1)
        {
            throw new ArgumentException("水平平滑系数必须在 (0, 1] 范围内", nameof(alpha));
        }

        if (beta is <= 0 or > 1)
        {
            throw new ArgumentException("趋势平滑系数必须在 (0, 1] 范围内", nameof(beta));
        }

        if (values.Length < 2)
        {
            throw new ArgumentException("至少需要2个数据点进行双重指数平滑", nameof(values));
        }

        var level = values[0];
        var trend = values[1] - values[0];

        for (var i = 1; i < values.Length; i++)
        {
            var newLevel = (alpha * values[i]) + ((1 - alpha) * (level + trend));
            var newTrend = (beta * (newLevel - level)) + ((1 - beta) * trend);

            level = newLevel;
            trend = newTrend;
        }

        return level + (periodsAhead * trend);
    }

    /// <summary>
    /// 多项式拟合预测
    /// </summary>
    /// <param name="xValues">自变量数据</param>
    /// <param name="yValues">因变量数据</param>
    /// <param name="degree">多项式次数</param>
    /// <param name="predictX">要预测的自变量值</param>
    /// <returns>预测值</returns>
    public static double PolynomialRegression(double[] xValues, double[] yValues, int degree, double predictX)
    {
        ArgumentNullException.ThrowIfNull(xValues);
        ArgumentNullException.ThrowIfNull(yValues);

        if (xValues.Length != yValues.Length)
        {
            throw new ArgumentException("自变量和因变量数据长度必须相同");
        }

        if (degree >= xValues.Length)
        {
            throw new ArgumentException("多项式次数必须小于数据点数量");
        }

        var coefficients = CalculatePolynomialCoefficients(xValues, yValues, degree);

        var result = 0.0;
        for (var i = 0; i <= degree; i++)
        {
            result += coefficients[i] * Math.Pow(predictX, i);
        }

        return result;
    }

    /// <summary>
    /// 季节性分解预测
    /// </summary>
    /// <param name="values">时间序列数据</param>
    /// <param name="seasonLength">季节长度</param>
    /// <returns>分解结果 (趋势, 季节性, 残差)</returns>
    public static (double[] trend, double[] seasonal, double[] residual) SeasonalDecomposition(double[] values, int seasonLength)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (seasonLength <= 0)
        {
            throw new ArgumentException("季节长度必须大于0", nameof(seasonLength));
        }

        if (values.Length < seasonLength * 2)
        {
            throw new ArgumentException("数据长度必须至少是季节长度的2倍", nameof(values));
        }

        var trend = CalculateTrend(values, seasonLength);
        var seasonal = CalculateSeasonality(values, trend, seasonLength);
        var residual = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            residual[i] = values[i] - trend[i] - seasonal[i];
        }

        return (trend, seasonal, residual);
    }

    /// <summary>
    /// 基于历史数据的趋势预测
    /// </summary>
    /// <param name="values">历史数据</param>
    /// <param name="periodsAhead">预测未来的周期数</param>
    /// <returns>预测值数组</returns>
    public static double[] TrendForecast(double[] values, int periodsAhead)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (periodsAhead <= 0)
        {
            throw new ArgumentException("预测周期数必须大于0", nameof(periodsAhead));
        }

        if (values.Length < 2)
        {
            throw new ArgumentException("至少需要2个数据点进行趋势预测", nameof(values));
        }

        var xValues = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();
        var (intercept, slope) = GetLinearRegressionCoefficients(xValues, values);

        var forecast = new double[periodsAhead];
        for (var i = 0; i < periodsAhead; i++)
        {
            forecast[i] = intercept + (slope * (values.Length + i));
        }

        return forecast;
    }

    /// <summary>
    /// 计算预测准确性指标
    /// </summary>
    /// <param name="actual">实际值</param>
    /// <param name="predicted">预测值</param>
    /// <returns>准确性指标 (MAE, MSE, RMSE, MAPE)</returns>
    public static AccuracyMetrics CalculateAccuracy(double[] actual, double[] predicted)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(predicted);

        if (actual.Length != predicted.Length)
        {
            throw new ArgumentException("实际值和预测值数组长度必须相同");
        }

        if (actual.Length == 0)
        {
            throw new ArgumentException("数组不能为空");
        }

        var n = actual.Length;
        var mae = 0.0; // 平均绝对误差
        var mse = 0.0; // 均方误差
        var mape = 0.0; // 平均绝对百分比误差

        for (var i = 0; i < n; i++)
        {
            var error = actual[i] - predicted[i];
            var absError = Math.Abs(error);

            mae += absError;
            mse += error * error;

            if (actual[i] != 0)
            {
                mape += Math.Abs(error / actual[i]);
            }
        }

        mae /= n;
        mse /= n;
        mape = mape / n * 100; // 转换为百分比
        var rmse = Math.Sqrt(mse);

        return new AccuracyMetrics
        {
            MAE = mae,
            MSE = mse,
            RMSE = rmse,
            MAPE = mape
        };
    }

    /// <summary>
    /// K近邻回归预测
    /// </summary>
    /// <param name="trainX">训练集自变量数据（支持多维特征）</param>
    /// <param name="trainY">训练集因变量数据</param>
    /// <param name="predictX">要预测的自变量值</param>
    /// <param name="k">邻居数量</param>
    /// <param name="distanceType">距离计算类型</param>
    /// <returns>预测值</returns>
    public static double KNearestNeighborsRegression(double[,] trainX, double[] trainY, double[] predictX, int k = 3, DistanceType distanceType = DistanceType.Euclidean)
    {
        ArgumentNullException.ThrowIfNull(trainX);
        ArgumentNullException.ThrowIfNull(trainY);
        ArgumentNullException.ThrowIfNull(predictX);

        var numSamples = trainX.GetLength(0);
        var numFeatures = trainX.GetLength(1);

        if (trainY.Length != numSamples)
        {
            throw new ArgumentException("训练集自变量和因变量数据长度必须相同");
        }

        if (predictX.Length != numFeatures)
        {
            throw new ArgumentException("预测数据的特征数量必须与训练数据一致");
        }

        if (k <= 0 || k > numSamples)
        {
            throw new ArgumentException($"K值必须在1到{numSamples}之间", nameof(k));
        }

        // 计算距离并排序
        var distances = new List<(double distance, double value)>();

        for (var i = 0; i < numSamples; i++)
        {
            var distance = CalculateDistance(trainX, predictX, i, distanceType);
            distances.Add((distance, trainY[i]));
        }

        // 选择K个最近邻
        var kNearest = distances.OrderBy(d => d.distance).Take(k);

        // 返回K个最近邻的平均值
        return kNearest.Average(x => x.value);
    }

    /// <summary>
    /// 决策树回归预测
    /// </summary>
    /// <param name="trainX">训练集自变量数据</param>
    /// <param name="trainY">训练集因变量数据</param>
    /// <param name="predictX">要预测的自变量值</param>
    /// <param name="maxDepth">最大深度</param>
    /// <param name="minSamplesSplit">节点分裂所需的最小样本数</param>
    /// <returns>预测值</returns>
    public static double DecisionTreeRegression(double[,] trainX, double[] trainY, double[] predictX, int maxDepth = 10, int minSamplesSplit = 2)
    {
        ArgumentNullException.ThrowIfNull(trainX);
        ArgumentNullException.ThrowIfNull(trainY);
        ArgumentNullException.ThrowIfNull(predictX);

        var numSamples = trainX.GetLength(0);
        var numFeatures = trainX.GetLength(1);

        if (trainY.Length != numSamples)
        {
            throw new ArgumentException("训练集自变量和因变量数据长度必须相同");
        }

        if (predictX.Length != numFeatures)
        {
            throw new ArgumentException("预测数据的特征数量必须与训练数据一致");
        }

        // 构建决策树
        var rootNode = BuildDecisionTree(trainX, trainY, [.. Enumerable.Range(0, numSamples)], 0, maxDepth, minSamplesSplit);

        // 进行预测
        return PredictWithDecisionTree(rootNode, predictX);
    }

    /// <summary>
    /// 随机森林回归预测
    /// </summary>
    /// <param name="trainX">训练集自变量数据</param>
    /// <param name="trainY">训练集因变量数据</param>
    /// <param name="predictX">要预测的自变量值</param>
    /// <param name="numTrees">树的数量</param>
    /// <param name="maxDepth">每棵树的最大深度</param>
    /// <param name="sampleRatio">每棵树使用的样本比例</param>
    /// <returns>预测值</returns>
    public static double RandomForestRegression(double[,] trainX, double[] trainY, double[] predictX, int numTrees = 10, int maxDepth = 10, double sampleRatio = 0.8)
    {
        ArgumentNullException.ThrowIfNull(trainX);
        ArgumentNullException.ThrowIfNull(trainY);
        ArgumentNullException.ThrowIfNull(predictX);

        var numSamples = trainX.GetLength(0);
        var numFeatures = trainX.GetLength(1);

        if (trainY.Length != numSamples)
        {
            throw new ArgumentException("训练集自变量和因变量数据长度必须相同");
        }

        if (predictX.Length != numFeatures)
        {
            throw new ArgumentException("预测数据的特征数量必须与训练数据一致");
        }

        if (sampleRatio is <= 0 or > 1)
        {
            throw new ArgumentException("样本比例必须在(0, 1]范围内", nameof(sampleRatio));
        }

        var random = new Random();
        var predictions = new List<double>();
        var sampleSize = (int)(numSamples * sampleRatio);

        // 构建多棵决策树
        for (var i = 0; i < numTrees; i++)
        {
            // Bootstrap采样
            var sampleIndices = new int[sampleSize];
            for (var j = 0; j < sampleSize; j++)
            {
                sampleIndices[j] = random.Next(numSamples);
            }

            // 构建决策树并预测
            var tree = BuildDecisionTree(trainX, trainY, sampleIndices, 0, maxDepth, 2);
            var prediction = PredictWithDecisionTree(tree, predictX);
            predictions.Add(prediction);
        }

        // 返回所有树预测结果的平均值
        return predictions.Average();
    }

    /// <summary>
    /// 梯度提升树回归预测
    /// </summary>
    /// <param name="trainX">训练集自变量数据</param>
    /// <param name="trainY">训练集因变量数据</param>
    /// <param name="predictX">要预测的自变量值</param>
    /// <param name="numIterations">迭代次数</param>
    /// <param name="learningRate">学习率</param>
    /// <param name="maxDepth">每棵树的最大深度</param>
    /// <returns>预测值</returns>
    public static double GradientBoostingRegression(double[,] trainX, double[] trainY, double[] predictX, int numIterations = 100, double learningRate = 0.1, int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(trainX);
        ArgumentNullException.ThrowIfNull(trainY);
        ArgumentNullException.ThrowIfNull(predictX);

        var numSamples = trainX.GetLength(0);
        var numFeatures = trainX.GetLength(1);

        if (trainY.Length != numSamples)
        {
            throw new ArgumentException("训练集自变量和因变量数据长度必须相同");
        }

        if (predictX.Length != numFeatures)
        {
            throw new ArgumentException("预测数据的特征数量必须与训练数据一致");
        }

        if (learningRate is <= 0 or > 1)
        {
            throw new ArgumentException("学习率必须在(0, 1]范围内", nameof(learningRate));
        }

        // 初始预测值为目标值的平均值
        var initialPrediction = trainY.Average();
        var currentPredictions = new double[numSamples];
        Array.Fill(currentPredictions, initialPrediction);

        var trees = new List<DecisionTreeNode>();

        // 梯度提升迭代
        for (var iter = 0; iter < numIterations; iter++)
        {
            // 计算残差（负梯度）
            var residuals = new double[numSamples];
            for (var i = 0; i < numSamples; i++)
            {
                residuals[i] = trainY[i] - currentPredictions[i];
            }

            // 构建回归树拟合残差
            var tree = BuildDecisionTree(trainX, residuals, [.. Enumerable.Range(0, numSamples)], 0, maxDepth, 2);
            trees.Add(tree);

            // 更新预测值
            for (var i = 0; i < numSamples; i++)
            {
                var sample = new double[numFeatures];
                for (var j = 0; j < numFeatures; j++)
                {
                    sample[j] = trainX[i, j];
                }
                currentPredictions[i] += learningRate * PredictWithDecisionTree(tree, sample);
            }
        }

        // 对新样本进行预测
        var finalPrediction = initialPrediction;
        foreach (var tree in trees)
        {
            finalPrediction += learningRate * PredictWithDecisionTree(tree, predictX);
        }

        return finalPrediction;
    }

    /// <summary>
    /// 自动选择最佳预测方法
    /// </summary>
    /// <param name="values">历史数据</param>
    /// <param name="testSize">用于测试的数据比例 (0-1)</param>
    /// <returns>最佳预测方法和其预测值</returns>
    public static (string method, double prediction, double accuracy) AutoSelectBestMethod(double[] values, double testSize = 0.2)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (testSize is <= 0 or >= 1)
        {
            throw new ArgumentException("测试数据比例必须在 (0, 1) 范围内", nameof(testSize));
        }

        if (values.Length < 10)
        {
            throw new ArgumentException("至少需要10个数据点进行自动方法选择", nameof(values));
        }

        var splitIndex = (int)(values.Length * (1 - testSize));
        var trainData = values.Take(splitIndex).ToArray();
        var testData = values.Skip(splitIndex).ToArray();

        var methods = new Dictionary<string, (double prediction, double accuracy)>();

        // 测试简单移动平均
        try
        {
            var smaPrediction = SimpleMovingAverage(trainData, Math.Min(5, trainData.Length));
            var smaAccuracy = CalculateAccuracyForSinglePrediction(testData[0], smaPrediction);
            methods["SimpleMovingAverage"] = (smaPrediction, smaAccuracy);
        }
        catch { /* 忽略错误 */ }

        // 测试指数平滑
        try
        {
            var esPrediction = ExponentialSmoothing(trainData, 0.3);
            var esAccuracy = CalculateAccuracyForSinglePrediction(testData[0], esPrediction);
            methods["ExponentialSmoothing"] = (esPrediction, esAccuracy);
        }
        catch { /* 忽略错误 */ }

        // 测试线性回归
        try
        {
            var xValues = Enumerable.Range(0, trainData.Length).Select(i => (double)i).ToArray();
            var lrPrediction = LinearRegression(xValues, trainData, trainData.Length);
            var lrAccuracy = CalculateAccuracyForSinglePrediction(testData[0], lrPrediction);
            methods["LinearRegression"] = (lrPrediction, lrAccuracy);
        }
        catch { /* 忽略错误 */ }

        // 测试KNN回归（将时间序列转换为特征矩阵）
        try
        {
            if (trainData.Length >= 4) // 至少需要足够的数据构建特征
            {
                var windowSize = Math.Min(3, trainData.Length / 2);
                var (trainX, trainY) = CreateTimeSeriesFeatures(trainData, windowSize);
                var predictFeatures = trainData.TakeLast(windowSize).ToArray();

                var knnPrediction = KNearestNeighborsRegression(trainX, trainY, predictFeatures, Math.Min(3, trainY.Length));
                var knnAccuracy = CalculateAccuracyForSinglePrediction(testData[0], knnPrediction);
                methods["KNearestNeighbors"] = (knnPrediction, knnAccuracy);
            }
        }
        catch { /* 忽略错误 */ }

        // 测试决策树回归
        try
        {
            if (trainData.Length >= 4)
            {
                var windowSize = Math.Min(3, trainData.Length / 2);
                var (trainX, trainY) = CreateTimeSeriesFeatures(trainData, windowSize);
                var predictFeatures = trainData.TakeLast(windowSize).ToArray();

                var dtPrediction = DecisionTreeRegression(trainX, trainY, predictFeatures, maxDepth: 5);
                var dtAccuracy = CalculateAccuracyForSinglePrediction(testData[0], dtPrediction);
                methods["DecisionTree"] = (dtPrediction, dtAccuracy);
            }
        }
        catch { /* 忽略错误 */ }

        // 选择准确度最高的方法
        var bestMethod = methods.OrderBy(m => m.Value.accuracy).First();
        return (bestMethod.Key, bestMethod.Value.prediction, bestMethod.Value.accuracy);
    }

    #region 私有辅助方法

    /// <summary>
    /// 计算多项式系数
    /// </summary>
    private static double[] CalculatePolynomialCoefficients(double[] xValues, double[] yValues, int degree)
    {
        var n = xValues.Length;
        var matrix = new double[degree + 1, degree + 2];

        // 构建增广矩阵
        for (var i = 0; i <= degree; i++)
        {
            for (var j = 0; j <= degree; j++)
            {
                matrix[i, j] = xValues.Sum(x => Math.Pow(x, i + j));
            }
            matrix[i, degree + 1] = xValues.Zip(yValues, (x, y) => y * Math.Pow(x, i)).Sum();
        }

        // 高斯消元法求解
        return SolveLinearSystem(matrix, degree + 1);
    }

    /// <summary>
    /// 求解线性方程组
    /// </summary>
    private static double[] SolveLinearSystem(double[,] matrix, int size)
    {
        // 前向消元
        for (var i = 0; i < size; i++)
        {
            // 选择主元
            var maxRow = i;
            for (var k = i + 1; k < size; k++)
            {
                if (Math.Abs(matrix[k, i]) > Math.Abs(matrix[maxRow, i]))
                {
                    maxRow = k;
                }
            }

            // 交换行
            for (var k = i; k <= size; k++)
            {
                (matrix[maxRow, k], matrix[i, k]) = (matrix[i, k], matrix[maxRow, k]);
            }

            // 消元
            for (var k = i + 1; k < size; k++)
            {
                var factor = matrix[k, i] / matrix[i, i];
                for (var j = i; j <= size; j++)
                {
                    matrix[k, j] -= factor * matrix[i, j];
                }
            }
        }

        // 回代求解
        var solution = new double[size];
        for (var i = size - 1; i >= 0; i--)
        {
            solution[i] = matrix[i, size];
            for (var j = i + 1; j < size; j++)
            {
                solution[i] -= matrix[i, j] * solution[j];
            }
            solution[i] /= matrix[i, i];
        }

        return solution;
    }

    /// <summary>
    /// 计算趋势分量
    /// </summary>
    private static double[] CalculateTrend(double[] values, int seasonLength)
    {
        var trend = new double[values.Length];
        var halfSeason = seasonLength / 2;

        for (var i = 0; i < values.Length; i++)
        {
            var start = Math.Max(0, i - halfSeason);
            var end = Math.Min(values.Length - 1, i + halfSeason);
            trend[i] = values.Skip(start).Take(end - start + 1).Average();
        }

        return trend;
    }

    /// <summary>
    /// 计算季节性分量
    /// </summary>
    private static double[] CalculateSeasonality(double[] values, double[] trend, int seasonLength)
    {
        var seasonal = new double[values.Length];
        var seasonalAvg = new double[seasonLength];

        // 计算每个季节位置的平均偏差
        for (var i = 0; i < seasonLength; i++)
        {
            var seasonValues = new List<double>();
            for (var j = i; j < values.Length; j += seasonLength)
            {
                seasonValues.Add(values[j] - trend[j]);
            }
            seasonalAvg[i] = seasonValues.Average();
        }

        // 应用季节性模式
        for (var i = 0; i < values.Length; i++)
        {
            seasonal[i] = seasonalAvg[i % seasonLength];
        }

        return seasonal;
    }

    /// <summary>
    /// 计算单个预测值的准确性
    /// </summary>
    private static double CalculateAccuracyForSinglePrediction(double actual, double predicted)
    {
        return Math.Abs(actual - predicted);
    }

    /// <summary>
    /// 计算两点间距离
    /// </summary>
    private static double CalculateDistance(double[,] trainX, double[] predictX, int sampleIndex, DistanceType distanceType)
    {
        var numFeatures = trainX.GetLength(1);
        double distance = 0;

        switch (distanceType)
        {
            case DistanceType.Euclidean:
                for (var i = 0; i < numFeatures; i++)
                {
                    var diff = trainX[sampleIndex, i] - predictX[i];
                    distance += diff * diff;
                }
                return Math.Sqrt(distance);

            case DistanceType.Manhattan:
                for (var i = 0; i < numFeatures; i++)
                {
                    distance += Math.Abs(trainX[sampleIndex, i] - predictX[i]);
                }
                return distance;

            case DistanceType.Chebyshev:
                for (var i = 0; i < numFeatures; i++)
                {
                    var diff = Math.Abs(trainX[sampleIndex, i] - predictX[i]);
                    distance = Math.Max(distance, diff);
                }
                return distance;

            default:
                throw new ArgumentException("不支持的距离类型", nameof(distanceType));
        }
    }

    /// <summary>
    /// 构建决策树
    /// </summary>
    private static DecisionTreeNode BuildDecisionTree(double[,] trainX, double[] trainY, int[] sampleIndices, int depth, int maxDepth, int minSamplesSplit)
    {
        var node = new DecisionTreeNode();

        // 计算当前节点的预测值（目标值的平均值）
        var targetValues = sampleIndices.Select(i => trainY[i]).ToArray();
        node.Value = targetValues.Average();

        // 停止条件
        if (depth >= maxDepth || sampleIndices.Length < minSamplesSplit || targetValues.All(y => Math.Abs(y - targetValues[0]) < 1e-10))
        {
            return node;
        }

        var bestFeature = -1;
        var bestThreshold = 0.0;
        var bestVarianceReduction = 0.0;
        var currentVariance = CalculateVariance(targetValues);

        var numFeatures = trainX.GetLength(1);

        // 寻找最佳分割特征和阈值
        for (var feature = 0; feature < numFeatures; feature++)
        {
            var featureValues = sampleIndices.Select(i => trainX[i, feature]).Distinct().OrderBy(v => v).ToArray();

            for (var i = 0; i < featureValues.Length - 1; i++)
            {
                var threshold = (featureValues[i] + featureValues[i + 1]) / 2;

                var leftIndices = sampleIndices.Where(idx => trainX[idx, feature] <= threshold).ToArray();
                var rightIndices = sampleIndices.Where(idx => trainX[idx, feature] > threshold).ToArray();

                if (leftIndices.Length == 0 || rightIndices.Length == 0)
                {
                    continue;
                }

                var leftTargets = leftIndices.Select(idx => trainY[idx]).ToArray();
                var rightTargets = rightIndices.Select(idx => trainY[idx]).ToArray();

                var leftVariance = CalculateVariance(leftTargets);
                var rightVariance = CalculateVariance(rightTargets);

                var weightedVariance = ((leftTargets.Length * leftVariance) + (rightTargets.Length * rightVariance)) / sampleIndices.Length;
                var varianceReduction = currentVariance - weightedVariance;

                if (varianceReduction > bestVarianceReduction)
                {
                    bestVarianceReduction = varianceReduction;
                    bestFeature = feature;
                    bestThreshold = threshold;
                }
            }
        }

        // 如果找到了有效的分割
        if (bestFeature != -1)
        {
            node.FeatureIndex = bestFeature;
            node.Threshold = bestThreshold;

            var leftIndices = sampleIndices.Where(idx => trainX[idx, bestFeature] <= bestThreshold).ToArray();
            var rightIndices = sampleIndices.Where(idx => trainX[idx, bestFeature] > bestThreshold).ToArray();

            node.Left = BuildDecisionTree(trainX, trainY, leftIndices, depth + 1, maxDepth, minSamplesSplit);
            node.Right = BuildDecisionTree(trainX, trainY, rightIndices, depth + 1, maxDepth, minSamplesSplit);
        }

        return node;
    }

    /// <summary>
    /// 使用决策树进行预测
    /// </summary>
    private static double PredictWithDecisionTree(DecisionTreeNode node, double[] features)
    {
        return node.Left == null && node.Right == null
            ? node.Value
            : features[node.FeatureIndex] <= node.Threshold
            ? PredictWithDecisionTree(node.Left!, features)
            : PredictWithDecisionTree(node.Right!, features);
    }

    /// <summary>
    /// 计算方差
    /// </summary>
    private static double CalculateVariance(double[] values)
    {
        if (values.Length <= 1)
        {
            return 0;
        }

        var mean = values.Average();
        var sumSquaredDiffs = values.Sum(v => (v - mean) * (v - mean));
        return sumSquaredDiffs / values.Length;
    }

    /// <summary>
    /// 创建时间序列特征矩阵（滑动窗口方法）
    /// </summary>
    private static (double[,] X, double[] y) CreateTimeSeriesFeatures(double[] timeSeries, int windowSize)
    {
        var numSamples = timeSeries.Length - windowSize;
        if (numSamples <= 0)
        {
            throw new ArgumentException("时间序列长度必须大于窗口大小");
        }

        var X = new double[numSamples, windowSize];
        var y = new double[numSamples];

        for (var i = 0; i < numSamples; i++)
        {
            for (var j = 0; j < windowSize; j++)
            {
                X[i, j] = timeSeries[i + j];
            }
            y[i] = timeSeries[i + windowSize];
        }

        return (X, y);
    }

    #endregion
}

/// <summary>
/// 预测准确性指标
/// </summary>
public class AccuracyMetrics
{
    /// <summary>
    /// 平均绝对误差 (Mean Absolute Error)
    /// </summary>
    public double MAE { get; set; }

    /// <summary>
    /// 均方误差 (Mean Squared Error)
    /// </summary>
    public double MSE { get; set; }

    /// <summary>
    /// 均方根误差 (Root Mean Squared Error)
    /// </summary>
    public double RMSE { get; set; }

    /// <summary>
    /// 平均绝对百分比误差 (Mean Absolute Percentage Error)
    /// </summary>
    public double MAPE { get; set; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的准确性指标</returns>
    public override string ToString()
    {
        return $"MAE: {MAE:F4}, MSE: {MSE:F4}, RMSE: {RMSE:F4}, MAPE: {MAPE:F2}%";
    }
}

/// <summary>
/// 决策树节点
/// </summary>
public class DecisionTreeNode
{
    /// <summary>
    /// 节点预测值
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 分割特征索引
    /// </summary>
    public int FeatureIndex { get; set; } = -1;

    /// <summary>
    /// 分割阈值
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 左子树
    /// </summary>
    public DecisionTreeNode? Left { get; set; }

    /// <summary>
    /// 右子树
    /// </summary>
    public DecisionTreeNode? Right { get; set; }

    /// <summary>
    /// 判断是否为叶子节点
    /// </summary>
    public bool IsLeaf => Left == null && Right == null;
}
