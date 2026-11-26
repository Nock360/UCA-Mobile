using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace UCA_Mobile.Engines
{
    public static class WolframBridgeEngine
    {
        public static bool TrySolveAdvancedMath(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            string clean = input.Trim();

            bool isExplicit = clean.StartsWith("wolfram:", StringComparison.OrdinalIgnoreCase) ||
                              clean.StartsWith("sympy:", StringComparison.OrdinalIgnoreCase);
            bool isLimit = clean.StartsWith("limit(", StringComparison.OrdinalIgnoreCase);

            if (!isExplicit && !isLimit) return false;

            string expr = (isExplicit ? clean.Substring(clean.IndexOf(':') + 1).Trim() : clean).Replace("^", "**");
            string pyScript = $@"
import sympy as sp
from sympy import symbols, sin, cos, tan, exp, log, sqrt, pi, oo, integrate, diff, limit, solve, series, erf, besselj
x, y, z, t, n, a, b = symbols('x y z t n a b')
try:
    res = {expr}
    print('OK:' + str(res))
except Exception as e:
    print('ERR:' + str(e))
";
            string tempPy = Path.Combine(Path.GetTempPath(), $"uca_sympy_{Guid.NewGuid():N}.py");
            try
            {
                File.WriteAllText(tempPy, pyScript, Encoding.UTF8);
                var psi = new ProcessStartInfo("python", $"\"{tempPy}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using var proc = Process.Start(psi);
                if (proc == null || !proc.WaitForExit(4000)) return false;

                string output = proc.StandardOutput.ReadToEnd().Trim();
                if (output.StartsWith("OK:"))
                {
                    string ans = output.Substring(3).Trim();
                    result = ans;
                    steps.Add("Выполнено через ядро фундаментальной алгебры (SymPy / Wolfram Mode):");
                    steps.Add($"Запрос: {expr}");
                    steps.Add($"Аналитический результат: {ans}");
                    return true;
                }
                else if (output.StartsWith("ERR:"))
                {
                    result = "Ошибка синтаксиса SymPy";
                    steps.Add(output.Substring(4));
                    return true;
                }
            }
            catch
            {
                if (isExplicit)
                {
                    result = "Требуется Python + SymPy";
                    steps.Add("Для режима wolfram: установите Python и выполните: pip install sympy");
                    return true;
                }
            }
            finally
            {
                if (File.Exists(tempPy)) File.Delete(tempPy);
            }
            return false;
        }
    }
}




