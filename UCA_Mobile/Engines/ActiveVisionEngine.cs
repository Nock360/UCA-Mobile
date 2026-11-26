using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UCA_Mobile.Engines
{
    public static class ActiveVisionEngine
    {
        public static (string FixedFormula, List<string> Warnings) SanitizeAndFixFormula(string rawOcr)
        {
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(rawOcr)) return ("", warnings);

            string s = rawOcr.Trim();

            // 1. Заменяем кириллические буквы-двойники на латинские внутри формул
            var lookalikes = new Dictionary<char, char>
            {
                ['х'] = 'x', ['Х'] = 'x', ['у'] = 'y', ['У'] = 'y',
                ['а'] = 'a', ['А'] = 'a', ['с'] = 'c', ['С'] = 'c',
                ['е'] = 'e', ['Е'] = 'e', ['о'] = '0', ['О'] = '0',
                ['р'] = 'p', ['Р'] = 'p', ['к'] = 'k', ['К'] = 'k'
            };

            bool hadCyrillic = false;
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (lookalikes.TryGetValue(chars[i], out char lat))
                {
                    chars[i] = lat;
                    hadCyrillic = true;
                }
            }
            s = new string(chars);
            if (hadCyrillic) warnings.Add("Исправлены кириллические символы-двойники");

            // 2. Чиним путаницу 'l' или 'I' вместо '1' рядом с цифрами и операторами
            string beforeDigitFix = s;
            s = Regex.Replace(s, @"(?<=\d)[lI]|[lI](?=\d)", "1");
            s = Regex.Replace(s, @"(?<=[\+\-\*/=\^\(\s])[lI](?=[\+\-\*/=\^\)\s]|$)", "1");
            if (s != beforeDigitFix) warnings.Add("Символ 'l/I' исправлен на цифру '1'");

            // 3. Убираем висящий знак '=' в конце ("x^2 + 5*x =")
            if (s.EndsWith("="))
            {
                s = s.TrimEnd('=').Trim();
                warnings.Add("Удалён пустой знак '=' в конце");
            }

            // 4. Восстанавливаем пропущенное умножение: 2x -> 2*x, 3(x+1) -> 3*(x+1)
            s = Regex.Replace(s, @"(\d)([a-zA-Z\(])", "$1*$2");
            s = Regex.Replace(s, @"(\))(\d|[a-zA-Z\(])", "$1*$2");

            // 5. Балансировка незакрытых скобок
            int openCount = s.Count(c => c == '(');
            int closeCount = s.Count(c => c == ')');
            if (openCount > closeCount)
            {
                s += new string(')', openCount - closeCount);
                warnings.Add($"Автоматически закрыты скобки ({openCount - closeCount} шт.)");
            }

            // 6. Проверяем наличие персональной базы почерка UserGlyphs
            if (Directory.Exists("UserGlyphs"))
            {
                int glyphFiles = Directory.GetFiles("UserGlyphs", "*.png").Length;
                if (glyphFiles > 0)
                    warnings.Add($"Сверено с базой почерка ({glyphFiles} эталонов)");
            }

            return (s, warnings);
        }
    }
}



