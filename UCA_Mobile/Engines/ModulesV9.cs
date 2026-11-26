using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace UCA_Mobile.Engines
{
    public static class BaseConverterEngine
    {
        public static bool TryConvertBase(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            var match = Regex.Match(input.Trim(), @"^(0x[0-9a-fA-F]+|0b[01]+|0o[0-7]+|\d+)\s+(?:to|в|->)\s+(bin|oct|dec|hex)$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            var g = match.Groups.Cast<Group>().ToList();
            string rawNum = g.ElementAt(1).Value.ToLowerInvariant();
            string target = g.ElementAt(2).Value.ToLowerInvariant();

            long decVal;
            if (rawNum.StartsWith("0x")) decVal = Convert.ToInt64(rawNum.Substring(2), 16);
            else if (rawNum.StartsWith("0b")) decVal = Convert.ToInt64(rawNum.Substring(2), 2);
            else if (rawNum.StartsWith("0o")) decVal = Convert.ToInt64(rawNum.Substring(2), 8);
            else decVal = long.Parse(rawNum);

            string bin = "0b" + Convert.ToString(decVal, 2);
            string oct = "0o" + Convert.ToString(decVal, 8);
            string hex = "0x" + Convert.ToString(decVal, 16).ToUpperInvariant();

            result = target switch { "bin" => bin, "oct" => oct, "hex" => hex, _ => decVal.ToString() };
            steps.Add($"DEC (10): {decVal}");
            steps.Add($"HEX (16): {hex}");
            steps.Add($"BIN (2):  {bin}");
            steps.Add($"OCT (8):  {oct}");
            return true;
        }
    }

    public static class StatisticsEngine
    {
        public static bool TryProcessStats(string input, out List<string> steps, out string result, out List<double> dataset)
        {
            steps = new List<string>();
            result = "";
            dataset = new List<double>();
            {
                string tStat = input.Trim();
                if (tStat.StartsWith("stats:", StringComparison.OrdinalIgnoreCase) || tStat.StartsWith("mean(", StringComparison.OrdinalIgnoreCase))
                {
                    var nums = System.Text.RegularExpressions.Regex.Matches(tStat, @"-?\d+(?:[.,]\d+)?")
                        .Select(m => double.Parse(m.Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture)).ToList();
                    if (nums.Count > 0)
                    {
                        double avg = nums.Average();
                        double v2 = nums.Average(v => Math.Pow(v - avg, 2));
                        double sd2 = Math.Sqrt(v2);
                        var srt = nums.OrderBy(x => x).ToList();
                        double med2 = srt.Count % 2 == 1 ? srt[srt.Count / 2] : (srt[srt.Count / 2 - 1] + srt[srt.Count / 2]) / 2.0;
                        dataset = nums;
                        steps.Add($"Выборка ({nums.Count} эл.): {string.Join(", ", nums)}");
                        steps.Add($"Среднее = {avg:F4} | Медиана = {med2:F4}");
                        steps.Add($"Дисперсия = {v2:F4} | СКО (σ) = {sd2:F4}");
                        result = $"Среднее = {avg:F3}, σ = {sd2:F3}, Медиана = {med2:F3}";
                        return true;
                    }
                }
            }
            var match = Regex.Match(input.Trim(), @"^stat\(([^)]+)\)$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            var rawItems = match.Groups.Cast<Group>().ElementAt(1).Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            dataset = rawItems.Select(x => double.Parse(x.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).OrderBy(x => x).ToList();
            if (dataset.Count == 0) return false;

            int n = dataset.Count;
            double mean = dataset.Average();
            double median = n % 2 == 1 ? dataset.ElementAt(n / 2) : (dataset.ElementAt(n / 2 - 1) + dataset.ElementAt(n / 2)) / 2.0;
            double variance = dataset.Sum(x => Math.Pow(x - mean, 2)) / n;
            double stdDev = Math.Sqrt(variance);

            result = $"Mean = {mean:G5}, Median = {median:G5}";
            steps.Add($"Объём выборки (N): {n}");
            steps.Add($"Минимум / Максимум: {dataset.First():G5} / {dataset.Last():G5}");
            steps.Add($"Среднее арифметическое: {mean:G6}");
            steps.Add($"Медиана: {median:G6}");
            steps.Add($"Дисперсия (σ²): {variance:G6}");
            steps.Add($"Стандартное отклонение (σ): {stdDev:G6}");
            return true;
        }
    }

    public static class FinanceEngine
    {
        public static bool TryProcessFinance(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            var match = Regex.Match(input.Trim(), @"^(credit|deposit)\(([^,]+),([^,]+),([^)]+)\)$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            var g = match.Groups.Cast<Group>().ToList();
            string mode = g.ElementAt(1).Value.ToLowerInvariant();
            double sum = double.Parse(g.ElementAt(2).Value.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
            double rateYear = double.Parse(g.ElementAt(3).Value.Replace("%", "").Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
            int months = int.Parse(g.ElementAt(4).Value.Trim());

            double rateMonth = rateYear / 100.0 / 12.0;

            if (mode == "credit")
            {
                double k = (rateMonth * Math.Pow(1 + rateMonth, months)) / (Math.Pow(1 + rateMonth, months) - 1);
                double monthly = sum * k;
                double total = monthly * months;
                double overpay = total - sum;

                result = $"Платёж: {monthly:N2} ₽/мес";
                steps.Add($"Сумма кредита: {sum:N2} ₽ на {months} мес. под {rateYear}%");
                steps.Add($"Ежемесячный платёж (аннуитет): {monthly:N2} ₽");
                steps.Add($"Общая сумма выплат: {total:N2} ₽");
                steps.Add($"Переплата по процентам: {overpay:N2} ₽");
            }
            else
            {
                double finalSum = sum * Math.Pow(1 + rateMonth, months);
                double profit = finalSum - sum;

                result = $"Итог: {finalSum:N2} ₽ (+{profit:N2} ₽)";
                steps.Add($"Начальный вклад: {sum:N2} ₽ на {months} мес. под {rateYear}%");
                steps.Add($"Итоговая сумма с капитализацией: {finalSum:N2} ₽");
                steps.Add($"Чистый доход по процентам: {profit:N2} ₽");
            }
            return true;
        }
    }
}



