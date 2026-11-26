using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;

namespace UCA_Mobile.Engines
{
    public static class UnitConverterEngine
    {
        private static readonly Dictionary<string, (string Category, double ToBase)> Units = new(StringComparer.OrdinalIgnoreCase)
        {
            ["m/s"] = ("speed", 1.0), ["km/h"] = ("speed", 1.0 / 3.6), ["mph"] = ("speed", 0.44704),
            ["rad"] = ("angle", 1.0), ["deg"] = ("angle", Math.PI / 180.0),
            ["pa"] = ("pressure", 1.0), ["kpa"] = ("pressure", 1e3), ["mpa"] = ("pressure", 1e6), ["bar"] = ("pressure", 1e5), ["atm"] = ("pressure", 101325.0),
            ["m"] = ("length", 1.0), ["cm"] = ("length", 0.01), ["mm"] = ("length", 0.001), ["km"] = ("length", 1000.0),
            ["m2"] = ("area", 1.0), ["cm2"] = ("area", 1e-4), ["km2"] = ("area", 1e6), ["ha"] = ("area", 10000.0),
            ["s"] = ("time", 1.0), ["min"] = ("time", 60.0), ["h"] = ("time", 3600.0), ["day"] = ("time", 86400.0),
            ["kg"] = ("mass", 1.0), ["g"] = ("mass", 0.001), ["t"] = ("mass", 1000.0),
            ["w"] = ("power", 1.0), ["kw"] = ("power", 1e3), ["hp"] = ("power", 735.49875),
            ["bit"] = ("data", 1.0), ["b"] = ("data", 8.0), ["kb"] = ("data", 8e3), ["mb"] = ("data", 8e6), ["gb"] = ("data", 8e9), ["tb"] = ("data", 8e12),
            ["kib"] = ("data", 8.0 * 1024), ["mib"] = ("data", 8.0 * 1048576), ["gib"] = ("data", 8.0 * 1073741824),
            ["bps"] = ("bitrate", 1.0), ["kbps"] = ("bitrate", 1e3), ["mbps"] = ("bitrate", 1e6), ["mb/s"] = ("bitrate", 8e6)
        };

        public static bool TryConvert(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            var match = Regex.Match(input.Trim(), @"^([+-]?\d+(?:[.,]\d+)?)\s*([a-zA-Z0-9/]+)\s+(?:to|в|->)\s+([a-zA-Z0-9/]+)$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            var groups = match.Groups.Cast<Group>().ToList();
            double val = double.Parse(groups.ElementAt(1).Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            string from = groups.ElementAt(2).Value;
            string to = groups.ElementAt(3).Value;

            if (TryConvertTemperature(val, from, to, out steps, out result)) return true;

            if (!Units.TryGetValue(from, out var uFrom) || !Units.TryGetValue(to, out var uTo))
                throw new Exception($"Неизвестная единица: {from} или {to}");

            if (uFrom.Category != uTo.Category)
                throw new Exception($"Несовместимые величины: {uFrom.Category} и {uTo.Category}");

            double baseVal = val * uFrom.ToBase;
            double converted = baseVal / uTo.ToBase;

            result = $"{converted:G6} {to}";
            steps.Add($"Исходное: {val} {from}");
            steps.Add($"В базовых единицах: {baseVal:G6}");
            steps.Add($"Результат: {val} {from} = {result}");
            return true;
        }

        private static bool TryConvertTemperature(double val, string from, string to, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            string f = from.ToUpperInvariant(), t = to.ToUpperInvariant();
            if (f != "C" && f != "F" && f != "K") return false;
            if (t != "C" && t != "F" && t != "K") return false;

            double celsius = f switch { "F" => (val - 32.0) * 5.0 / 9.0, "K" => val - 273.15, _ => val };
            double res = t switch { "F" => celsius * 9.0 / 5.0 + 32.0, "K" => celsius + 273.15, _ => celsius };

            result = $"{res:G6} °{t}";
            steps.Add($"Исходная температура: {val} °{f}");
            steps.Add($"Перевод через Цельсий: {celsius:G6} °C");
            steps.Add($"Результат: {result}");
            return true;
        }
    }
    public static class MatrixEngine
    {
        public static bool TryProcessMatrix(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            string clean = input.Trim();
            if (!clean.Contains("[[") || !clean.Contains("]]")) return false;

            var matches = Regex.Matches(clean, @"\[\[(?:[^\[\]]|\],\[)+\]\]").Cast<Match>().ToList();
            if (matches.Count == 2)
            {
                var m1 = ParseSingleMatrix(matches.ElementAt(0).Value);
                var m2 = ParseSingleMatrix(matches.ElementAt(1).Value);
                int startBetween = matches.ElementAt(0).Index + matches.ElementAt(0).Length;
                string between = clean.Substring(startBetween, matches.ElementAt(1).Index - startBetween).Trim();

                List<List<double>> resMat;
                if (between == "*") { resMat = Multiply(m1, m2); steps.Add("Умножение матриц (A * B):"); }
                else if (between == "+") { resMat = AddSub(m1, m2, 1); steps.Add("Сложение матриц (A + B):"); }
                else if (between == "-") { resMat = AddSub(m1, m2, -1); steps.Add("Вычитание матриц (A - B):"); }
                else throw new Exception("Поддерживаются операции между матрицами: *, +, -");

                steps.Add(FormatMatrix(resMat));
                result = $"Результат ({resMat.Count}x{resMat.First().Count})";
                return true;
            }

            var m = ParseSingleMatrix(matches.ElementAt(0).Value);
            int rows = m.Count, cols = m.First().Count;
            int rank = ComputeRank(m);
            steps.Add($"Матрица {rows}x{cols}:");
            steps.Add(FormatMatrix(m));
            steps.Add($"Ранг матрицы rank(A) = {rank}");
            steps.Add("Транспонированная (A^T):");
            steps.Add(FormatMatrix(Transpose(m)));

            if (rows == cols)
            {
                double det = Determinant(m);
                double trace = Enumerable.Range(0, rows).Sum(i => m.ElementAt(i).ElementAt(i));
                steps.Add($"След матрицы trace(A) = {trace:G6}");
                steps.Add($"Определитель det(A) = {det:G6}");
                result = $"det = {det:G6}, rank = {rank}";

                if (Math.Abs(det) > 1e-10)
                {
                    steps.Add("Обратная матрица (A^-1):");
                    steps.Add(FormatMatrix(Inverse(m)));
                }
            }
            else result = $"Матрица {rows}x{cols}, rank = {rank}";

            return true;
        }
        private static List<List<double>> ParseSingleMatrix(string matStr)
        {
            string body = matStr.Substring(2, matStr.Length - 4);
            var rowsRaw = body.Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);
            int cols = rowsRaw.First().Split(',').Length;
            var m = new List<List<double>>();
            foreach (var r in rowsRaw)
            {
                var items = r.Split(',');
                if (items.Length != cols) throw new Exception("Строки матрицы разной длины.");
                m.Add(items.Select(x => double.Parse(x.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToList());
            }
            return m;
        }

        private static List<List<double>> AddSub(List<List<double>> a, List<List<double>> b, int sign)
        {
            if (a.Count != b.Count || a.First().Count != b.First().Count) throw new Exception("Размеры матриц не совпадают.");
            return a.Select((row, i) => row.Select((val, j) => val + sign * b.ElementAt(i).ElementAt(j)).ToList()).ToList();
        }

        private static List<List<double>> Multiply(List<List<double>> a, List<List<double>> b)
        {
            int rA = a.Count, cA = a.First().Count, rB = b.Count, cB = b.First().Count;
            if (cA != rB) throw new Exception($"Нельзя умножить {rA}x{cA} на {rB}x{cB}.");
            var res = new List<List<double>>();
            for (int i = 0; i < rA; i++)
            {
                var row = new List<double>();
                for (int j = 0; j < cB; j++)
                    row.Add(Enumerable.Range(0, cA).Sum(k => a.ElementAt(i).ElementAt(k) * b.ElementAt(k).ElementAt(j)));
                res.Add(row);
            }
            return res;
        }

        private static int ComputeRank(List<List<double>> m)
        {
            int r = m.Count, c = m.First().Count, rank = 0;
            var mat = m.Select(row => row.ToList()).ToList();
            var rowUsed = Enumerable.Repeat(false, r).ToList();
            for (int j = 0; j < c; j++)
            {
                int i = 0;
                while (i < r && (rowUsed.ElementAt(i) || Math.Abs(mat.ElementAt(i).ElementAt(j)) < 1e-9)) i++;
                if (i < r)
                {
                    rank++;
                    rowUsed.RemoveAt(i); rowUsed.Insert(i, true);
                    double pivot = mat.ElementAt(i).ElementAt(j);
                    for (int p = j; p < c; p++) { double v = mat.ElementAt(i).ElementAt(p) / pivot; mat.ElementAt(i).RemoveAt(p); mat.ElementAt(i).Insert(p, v); }
                    for (int k = 0; k < r; k++)
                    {
                        if (k != i && Math.Abs(mat.ElementAt(k).ElementAt(j)) > 1e-9)
                        {
                            double factor = mat.ElementAt(k).ElementAt(j);
                            for (int p = j; p < c; p++) { double v = mat.ElementAt(k).ElementAt(p) - factor * mat.ElementAt(i).ElementAt(p); mat.ElementAt(k).RemoveAt(p); mat.ElementAt(k).Insert(p, v); }
                        }
                    }
                }
            }
            return rank;
        }

        private static string FormatMatrix(List<List<double>> m) =>
            string.Join("\n", m.Select(r => "[ " + string.Join("  ", r.Select(v => $"{Math.Round(v, 4),7}")) + " ]"));

        private static List<List<double>> Transpose(List<List<double>> m)
        {
            int r = m.Count, c = m.First().Count;
            return Enumerable.Range(0, c).Select(j => Enumerable.Range(0, r).Select(i => m.ElementAt(i).ElementAt(j)).ToList()).ToList();
        }

        private static double Determinant(List<List<double>> m)
        {
            int n = m.Count;
            if (n == 1) return m.First().First();
            if (n == 2) return m.ElementAt(0).ElementAt(0) * m.ElementAt(1).ElementAt(1) - m.ElementAt(0).ElementAt(1) * m.ElementAt(1).ElementAt(0);
            double det = 0;
            for (int p = 0; p < n; p++)
            {
                var sub = Enumerable.Range(1, n - 1).Select(i => Enumerable.Range(0, n).Where(j => j != p).Select(j => m.ElementAt(i).ElementAt(j)).ToList()).ToList();
                det += (p % 2 == 0 ? 1 : -1) * m.First().ElementAt(p) * Determinant(sub);
            }
            return det;
        }

        private static List<List<double>> Inverse(List<List<double>> m)
        {
            int n = m.Count; double det = Determinant(m);
            if (n == 1) return new List<List<double>> { new List<double> { 1.0 / det } };
            var adj = Enumerable.Range(0, n).Select(_ => Enumerable.Repeat(0.0, n).ToList()).ToList();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var sub = Enumerable.Range(0, n).Where(r => r != i).Select(r => Enumerable.Range(0, n).Where(c => c != j).Select(c => m.ElementAt(r).ElementAt(c)).ToList()).ToList();
                    double val = ((i + j) % 2 == 0 ? 1 : -1) * Determinant(sub) / det;
                    adj.ElementAt(j).RemoveAt(i); adj.ElementAt(j).Insert(i, val);
                }
            return adj;
        }
    }

}
