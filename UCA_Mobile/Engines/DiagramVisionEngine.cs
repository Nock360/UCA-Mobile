using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Newtonsoft.Json;

namespace UCA_Mobile.Engines
{
    public static class DiagramVisionEngine
    {
        public static bool TrySolveGeometryText(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            result = "";
            string clean = input.Trim();
            if (!clean.StartsWith("geom:", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("геом:", StringComparison.OrdinalIgnoreCase))
                return false;

            var p = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in clean.Substring(clean.IndexOf(':') + 1).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && double.TryParse(kv.Last().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                    p[kv.First().Trim()] = v;
            }

            if (p.ContainsKey("r"))
            {
                double r = p["r"];
                double c = 2 * Math.PI * r, s = Math.PI * r * r, vSphere = (4.0 / 3.0) * Math.PI * r * r * r;
                result = $"Окружность R={r}: S = {s:F3}, Длина L = {c:F3}";
                steps.Add($"Геометрический расчёт окружности / сферы (R = {r}):");
                steps.Add($"Длина окружности L = 2πR = {c:F4} | Площадь круга S = πR² = {s:F4}");
                steps.Add($"Объём шара V = (4/3)πR³ = {vSphere:F4}");
                return true;
            }

            if (p.ContainsKey("a") && p.ContainsKey("b") && p.ContainsKey("gamma"))
            {
                double a = p["a"], b = p["b"], gDeg = p["gamma"];
                double gRad = gDeg * Math.PI / 180.0;
                double c = Math.Sqrt(a * a + b * b - 2 * a * b * Math.Cos(gRad));
                double s = 0.5 * a * b * Math.Sin(gRad);
                double perim = a + b + c;
                double alpha = Math.Acos(Math.Clamp((b * b + c * c - a * a) / (2 * b * c), -1, 1)) * 180.0 / Math.PI;
                double beta = 180.0 - gDeg - alpha;

                result = $"Треугольник: c = {c:F3}, S = {s:F3}, α = {alpha:F1}°, β = {beta:F1}°";
                steps.Add($"Решение треугольника по двум сторонам и углу (a = {a}, b = {b}, γ = {gDeg}°):");
                steps.Add($"Третья сторона (теорема косинусов): c = √(a² + b² - 2ab·cos γ) = {c:F4}");
                steps.Add($"Углы треугольника: α = {alpha:F2}°, β = {beta:F2}°, γ = {gDeg:F2}°");
                steps.Add($"Площадь S = ½ab·sin γ = {s:F4} | Периметр P = {perim:F4}");
                return true;
            }

            return false;
        }

        public static async Task<(bool Success, List<string> Steps, string Result)> AnalyzeDiagramImageAsync(BitmapSource img, string aiUrl)
        {
            var steps = new List<string>();
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                string visionModel = "";
                try
                {
                    dynamic? tags = JsonConvert.DeserializeObject(await client.GetStringAsync("http://localhost:11434/api/tags"));
                    var models = new List<string>();
                    if (tags?.models != null)
                        foreach (var item in tags.models) models.Add((string)item.name);

                    visionModel = models.FirstOrDefault(m =>
                        m.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("llava", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("minicpm", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("vl", StringComparison.OrdinalIgnoreCase)) ?? "";
                }
                catch { }

                if (string.IsNullOrEmpty(visionModel))
                {
                    steps.Add("Для анализа чертежей и электросхем по фото установите визуальную модель в Ollama:");
                    steps.Add("Выполните в терминале: ollama pull minicpm-v  (или  ollama pull llama3.2-vision)");
                    return (false, steps, "Vision-модель не установлена");
                }

                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(img));
                using var ms = new MemoryStream();
                enc.Save(ms);
                string b64 = Convert.ToBase64String(ms.ToArray());

                string prompt =
                    "Ты — инженер-анализатор чертежей и электрических схем. Внимательно изучи это изображение:\n" +
                    "1. Определи тип схемы (геометрический чертёж: треугольник/окружность/фигура ИЛИ электрическая цепь: резисторы, конденсаторы, источники ЭДС).\n" +
                    "2. Выпиши все данные с картинки (стороны, углы, номиналы резисторов в Омах, напряжение в Вольтах, тип соединения).\n" +
                    "3. Выполни полный пошаговый расчёт всех неизвестных величин (углы, площади, эквивалентное сопротивление, токи, напряжения).\n" +
                    "Отвечай на русском языке, чётко, без сложного LaTeX-кода.";

                var payload = new { model = visionModel, prompt = prompt, images = new[] { b64 }, stream = false, options = new { temperature = 0.2 } };
                var resp = await client.PostAsync(aiUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
                dynamic? json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
                string raw = SmartInputAndFormat.BeautifyMath((string)(json?.response ?? ""));

                steps.Add($"Анализ чертежа / схемы выполнен ({visionModel}):");
                steps.AddRange(raw.Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
                return (true, steps, $"Схема проанализирована ({visionModel})");
            }
            catch (Exception ex)
            {
                steps.Add($"Ошибка анализа схемы: {ex.Message}");
                return (false, steps, "Сбой анализа схемы");
            }
        }
    }
}



