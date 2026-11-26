using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UCA_Mobile.Engines
{
    public static class MobileMatrixEngine
    {
        public static bool TrySolveMatrix(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            string t = input.Trim();
            if (!t.StartsWith("det", StringComparison.OrdinalIgnoreCase) && !t.StartsWith("matrix", StringComparison.OrdinalIgnoreCase))
                return false;

            string body = t.Substring(t.IndexOf(' ') + 1).Trim();
            var rows = body.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => double.Parse(x.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToList())
                .ToList();

            if (rows.Count == 2 && rows.ElementAt(0).Count == 2 && rows.ElementAt(1).Count == 2)
            {
                double a = rows.ElementAt(0).ElementAt(0);
                double b = rows.ElementAt(0).ElementAt(1);
                double c = rows.ElementAt(1).ElementAt(0);
                double d = rows.ElementAt(1).ElementAt(1);
                double det = a * d - b * c;
                steps.Add($"Матрица 2x2: ({a} * {d}) - ({b} * {c}) = {det}");
                result = $"det(A) = {det}";
                return true;
            }
            if (rows.Count == 3 && rows.All(r => r.Count == 3))
            {
                double a00 = rows.ElementAt(0).ElementAt(0), a01 = rows.ElementAt(0).ElementAt(1), a02 = rows.ElementAt(0).ElementAt(2);
                double a10 = rows.ElementAt(1).ElementAt(0), a11 = rows.ElementAt(1).ElementAt(1), a12 = rows.ElementAt(1).ElementAt(2);
                double a20 = rows.ElementAt(2).ElementAt(0), a21 = rows.ElementAt(2).ElementAt(1), a22 = rows.ElementAt(2).ElementAt(2);
                double det = a00 * (a11 * a22 - a12 * a21) - a01 * (a10 * a22 - a12 * a20) + a02 * (a10 * a21 - a11 * a20);
                steps.Add($"Матрица 3x3: det = {det}");
                result = $"det(A) = {det}";
                return true;
            }
            return false;
        }
    }
}
