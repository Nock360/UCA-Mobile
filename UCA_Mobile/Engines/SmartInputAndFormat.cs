using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UCA_Mobile.Engines
{
    public static class SmartInputAndFormat
    {
        public static string BeautifyMath(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string s = text;
            s = Regex.Replace(s, @"\\\[|\\\]|\\\(|\\\)|\$\$|\$", "");
            s = Regex.Replace(s, @"\\sum_\{([^{}]+)\}\^\{([^{}]+)\}", "Σ (от $1 до $2) ");
            for (int i = 0; i < 3; i++)
                s = Regex.Replace(s, @"\\frac\{([^{}]+)\}\{([^{}]+)\}", "($1) / ($2)");
            s = Regex.Replace(s, @"\\sqrt\{([^{}]+)\}", "√($1)");
            s = Regex.Replace(s, @"\\text\{([^{}]+)\}", "$1");
            s = Regex.Replace(s, @"\^\{([^{}]+)\}", "^$1");
            s = Regex.Replace(s, @"_\{([^{}]+)\}", "_$1");
            s = s.Replace(@"\times", "×").Replace(@"\cdot", "·").Replace(@"\approx", "≈")
                 .Replace(@"\pi", "π").Replace(@"\infty", "∞").Replace(@"\pm", "±")
                 .Replace(@"\leq", "≤").Replace(@"\geq", "≥").Replace(@"\neq", "≠")
                 .Replace(@"\alpha", "α").Replace(@"\beta", "β").Replace(@"\omega", "ω")
                 .Replace(@"\sum", "Σ");
            return Regex.Replace(s, @"[ \t]{2,}", " ").Trim();
        }

        public static string NormalizeUserQuery(string raw)
        {
            string s = raw.Trim();
            if (string.IsNullOrWhiteSpace(s)) return s;

            // Свободный ввод: интеграл x^2 от 0 до 3
            var mInt = Regex.Match(s, @"^(?:интеграл|integral)\s+(.+?)\s+(?:от|from)\s+([+-]?\d+(?:[.,]\d+)?)\s+(?:до|to)\s+([+-]?\d+(?:[.,]\d+)?)$", RegexOptions.IgnoreCase);
            if (mInt.Success) return $"int({mInt.Groups[1].Value}, {mInt.Groups[2].Value.Replace(',', '.')}, {mInt.Groups[3].Value.Replace(',', '.')})";

            // Свободный ввод: кредит / вклад
            var mFin = Regex.Match(s, @"^(кредит|вклад|депозит)\s+(\d+(?:[.,]\d+)?)\s+(?:под\s+)?(\d+(?:[.,]\d+)?)%?\s+(?:на\s+)?(\d+)", RegexOptions.IgnoreCase);
            if (mFin.Success)
            {
                string cmd = mFin.Groups[1].Value.StartsWith("к", StringComparison.OrdinalIgnoreCase) ? "credit" : "deposit";
                return $"{cmd}({mFin.Groups[2].Value}, {mFin.Groups[3].Value}%, {mFin.Groups[4].Value})";
            }

            // Свободный ввод: статистика 10 20 30 40
            var mStat = Regex.Match(s, @"^(?:статистика|выборка|stat)\s+([0-9.,;\s-]+)$", RegexOptions.IgnoreCase);
            if (mStat.Success)
            {
                var nums = Regex.Split(mStat.Groups[1].Value.Trim(), @"[\s,;]+").Where(x => x.Length > 0);
                return $"stat({string.Join(", ", nums)})";
            }

            // Свободный ввод: резисторы 10 20 30
            var mRes = Regex.Match(s, @"^(?:резисторы|сопротивления)\s+([0-9.,;\s]+)$", RegexOptions.IgnoreCase);
            if (mRes.Success)
            {
                var nums = Regex.Split(mRes.Groups[1].Value.Trim(), @"[\s,;]+").Where(x => x.Length > 0);
                return $"res({string.Join(", ", nums)})";
            }

            // Свободный ввод: контур L=0.05 C=10e-6 | закон ома U=12 R=4 | бросок v0=30 угол 45
            if (Regex.IsMatch(s, @"^(?:контур|lc)\s+", RegexOptions.IgnoreCase))
                return "lc(" + Regex.Replace(s, @"^(?:контур|lc)\s+", "", RegexOptions.IgnoreCase).Replace(" ", ", ") + ")";

            if (Regex.IsMatch(s, @"^(?:закон ома|ом|ohm)\s+", RegexOptions.IgnoreCase))
                return "ohm(" + Regex.Replace(s, @"^(?:закон ома|ом|ohm)\s+", "", RegexOptions.IgnoreCase).Replace(" ", ", ") + ")";

            if (Regex.IsMatch(s, @"^(?:бросок|баллистика|throw)\s+", RegexOptions.IgnoreCase))
            {
                string body = Regex.Replace(s, @"^(?:бросок|баллистика|throw)\s+", "", RegexOptions.IgnoreCase)
                                   .Replace("угол=", "angle=").Replace("угол ", "angle=").Replace("скорость=", "v0=").Replace("скорость ", "v0=");
                return "throw(" + string.Join(", ", body.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)) + ")";
            }

            // Свободный ввод: монте-карло 50000
            var mMc = Regex.Match(s, @"^(?:монте[- ]?карло|montecarlo)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (mMc.Success) return $"montecarlo({mMc.Groups[1].Value})";

            // Свободный ввод систем счисления: 255 в двоичную / шестнадцатеричную / восьмеричную
            s = Regex.Replace(s, @"\s+(?:в|to)\s+(?:двоичн\w*|bin)\b", " to bin", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+(?:в|to)\s+(?:шестнадцатеричн\w*|hex)\b", " to hex", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+(?:в|to)\s+(?:восьмеричн\w*|oct)\b", " to oct", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+(?:в|to)\s+(?:десятичн\w*|dec)\b", " to dec", RegexOptions.IgnoreCase);

            return s;
        }
    }
}




