using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngouriMath;
using AngouriMath.Extensions;
using Newtonsoft.Json;

namespace UCA_Mobile.Engines
{
    public static class BatchExamEngine
    {
        public static bool IsBatchTicket(string input)
        {
            string t = input.Trim();
            if (t.StartsWith("вариант:", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("билет:", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("batch:", StringComparison.OrdinalIgnoreCase))
                return true;

            var lines = t.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int numbered = lines.Count(l => Regex.IsMatch(l.Trim(), @"^(\d+[\.\)]|Задача\s*\d+|№\s*\d+)", RegexOptions.IgnoreCase));
            return numbered >= 2;
        }

        public static List<string> SplitTasks(string input)
        {
            string body = Regex.Replace(input.Trim(), @"^(вариант|билет|batch)\s*:\s*", "", RegexOptions.IgnoreCase);
            string splitReady = Regex.Replace(body, @"\s+(?=\d+[\.\)]\s)", "`n"); var lines = splitReady.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

            var tasks = new List<string>();
            var current = new StringBuilder();

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"^(\d+[\.\)]|Задача\s*\d+|№\s*\d+)", RegexOptions.IgnoreCase) && current.Length > 0)
                {
                    tasks.Add(current.ToString().Trim());
                    current.Clear();
                }
                current.AppendLine(line);
            }
            if (current.Length > 0) tasks.Add(current.ToString().Trim());
            return tasks;
        }

        public static async Task<List<string>> SolveTicketAsync(string rawInput, string aiUrl, string defaultModel, Action<string> onProgress)
        {
            var tasks = SplitTasks(rawInput);
            var results = new List<string> { $"ПАКЕТНЫЙ РАЗБОР ВАРИАНТА (Всего заданий: {tasks.Count})" };

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            string activeModel = defaultModel;
            try
            {
                var tagsResp = await client.GetStringAsync("http://localhost:11434/api/tags");
                dynamic? tagsObj = JsonConvert.DeserializeObject(tagsResp);
                var models = new List<string>();
                if (tagsObj?.models != null)
                    foreach (var item in tagsObj.models) models.Add((string)item.name);
                if (models.Count > 0 && !models.Contains(activeModel)) activeModel = models.First();
            }
            catch { }

            for (int i = 0; i < tasks.Count; i++)
            {
                string task = tasks.ElementAt(i);
                onProgress($"Решаю задание {i + 1} из {tasks.Count}...");
                string cleanTask = Regex.Replace(task, @"^(\d+[\.\)]|Задача\s*\d+|№\s*\d+)\s*", "", RegexOptions.IgnoreCase).Trim();
                string norm = SmartInputAndFormat.NormalizeUserQuery(cleanTask);

                if (PhysicsLabEngine.TryProcessPhysics(norm, out var pSteps, out var pRes, out _))
                {
                    results.Add($"=== Задание {i + 1}: {task} ===\nОтвет: {pRes}\n" + string.Join("\n", pSteps));
                    continue;
                }
                if (UnitConverterEngine.TryConvert(norm, out var cSteps, out var cRes))
                {
                    results.Add($"=== Задание {i + 1}: {task} ===\nОтвет: {cRes}\n" + string.Join("\n", cSteps));
                    continue;
                }
                if (BaseConverterEngine.TryConvertBase(norm, out var bSteps, out var bRes))
                {
                    results.Add($"=== Задание {i + 1}: {task} ===\nОтвет: {bRes}\n" + string.Join("\n", bSteps));
                    continue;
                }

                // Пробуем точную алгебру для коротких формул
                try
                {
                    string mathClean = norm.Replace(" ", "");
                    if (!mathClean.Any(c => c >= 'а' && c <= 'я') && mathClean.Length < 45)
                    {
                        if (mathClean.Contains("="))
                        {
                            var pts = mathClean.Split('=');
                            Entity eq = (Entity)pts.First() - (Entity)pts.Last();
                            results.Add($"=== Задание {i + 1}: {task} ===\nТочный ответ: x ∈ {eq.SolveEquation("x")}");
                            continue;
                        }
                        Entity ex = mathClean;
                        results.Add($"=== Задание {i + 1}: {task} ===\nТочный ответ: {ex.Simplify()}");
                        continue;
                    }
                }
                catch { }

                // Иначе решаем через локальный ИИ с учётом методики лекции
                try
                {
                    string basePrompt = "Отвечай кратко, по шагам, простым языком и БЕЗ LaTeX-разметки (без \\frac, \\sqrt, \\sum). Дай чёткий ответ в конце.\n\nЗадача:\n" + task;
                    string finalPrompt = LectureAnalyzerEngine.ApplyLectureStyleToPrompt(basePrompt, task);
                    var payload = new { model = activeModel, prompt = finalPrompt, stream = false, options = new { temperature = 0.25 } };
                    var resp = await client.PostAsync(aiUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
                    dynamic? json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
                    string aiAns = SmartInputAndFormat.BeautifyMath((string)(json?.response ?? "Нет ответа"));
                    results.Add($"=== Задание {i + 1}: {task} ===\n{aiAns}");
                }
                catch
                {
                    results.Add($"=== Задание {i + 1}: {task} ===\n[Ошибка: локальный сервер Ollama не запущен]");
                }
            }

            return results;
        }
    }
}





