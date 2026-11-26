using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace UCA_Mobile.Engines
{
    public class NotebookExample
    {
        public string Topic { get; set; } = "";
        public string UserHandwritingSample { get; set; } = "";
    }

    public class HistoryRecord
    {
        public string Timestamp { get; set; } = "";
        public string Query { get; set; } = "";
        public string Result { get; set; } = "";
        public string DisplayTitle => $"[{Timestamp}] {Query.Replace("\r", " ").Replace("\n", " ")}";
    }

    public class StyleMemoryEngine
    {
        private const string MemoryFilePath = "my_notebook_style.json";
        private const string HistoryFilePath = "quantum_history.json";

        public List<NotebookExample> SavedExamples { get; private set; } = new();
        public List<HistoryRecord> History { get; private set; } = new();

        public StyleMemoryEngine()
        {
            if (File.Exists(MemoryFilePath))
                SavedExamples = JsonConvert.DeserializeObject<List<NotebookExample>>(File.ReadAllText(MemoryFilePath)) ?? new();
            if (File.Exists(HistoryFilePath))
                History = JsonConvert.DeserializeObject<List<HistoryRecord>>(File.ReadAllText(HistoryFilePath)) ?? new();
        }

        public void AddSampleFromNotebook(string textSample)
        {
            if (string.IsNullOrWhiteSpace(textSample)) return;
            SavedExamples.Add(new NotebookExample
            {
                Topic = $"Образец {DateTime.Now:dd.MM HH:mm}",
                UserHandwritingSample = textSample.Trim()
            });
            if (SavedExamples.Count > 5) SavedExamples.RemoveAt(0);
            File.WriteAllText(MemoryFilePath, JsonConvert.SerializeObject(SavedExamples, Formatting.Indented));
        }

        public void SaveToHistory(string query, string result)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(result)) return;
            History.Insert(0, new HistoryRecord
            {
                Timestamp = DateTime.Now.ToString("dd.MM HH:mm"),
                Query = query.Trim(),
                Result = result.Trim()
            });
            if (History.Count > 40) History.RemoveAt(History.Count - 1);
            File.WriteAllText(HistoryFilePath, JsonConvert.SerializeObject(History, Formatting.Indented));
        }

        public string BuildHumanizedPrompt(string newTask)
        {
            string styleBlock = "";
            if (SavedExamples.Count > 0)
            {
                styleBlock = "ПРИМЕРЫ МОЕГО ОФОРМЛЕНИЯ В ТЕТРАДИ (СКОПИРУЙ МОЙ СТИЛЬ):\n";
                foreach (var ex in SavedExamples)
                    styleBlock += $"---\n{ex.UserHandwritingSample}\n";
            }

            return $@"Ты - студент, решающий задачу в свою тетрадь. Владеешь всей математикой и физикой.
ПРАВИЛА МАСКИРОВКИ (ЧТОБЫ НЕ БЫЛО ВИДНО ИИ):
1. Никаких приветствий и вступлений. Сразу пиши решение.
2. Запрещены символы Markdown (никаких **, ###, •, $). Только чистый текст.
3. Используй живые сокращения: 'Дано:', 'Пусть...', 'Т.к.', 'Следовательно,', 'ОДЗ:', 'Ответ:'.
{styleBlock}
ЗАДАЧА:
{newTask}";
        }

        public string BuildErrorDetectivePrompt(string studentDraft)
        {
            return $@"Ты - внимательный репетитор. Проверь черновик решения ученика.
Найди КОНКРЕТНУЮ строчку, где допущена арифметическая, логическая или знака-ошибка.
Пиши кратко, без воды и без символов Markdown (без ** и #).
Укажи:
1. Где именно ошибка (цитата строки).
2. Почему это неверно.
3. Как исправить и какой правильный ответ.

ЧЕРНОВИК УЧЕНИКА:
{studentDraft}";
        }

        public string CleanAiArtifacts(string rawOutput)
        {
            string cleaned = rawOutput.Replace("**", "").Replace("###", "").Replace("##", "");
            cleaned = cleaned.Replace("—", "-").Replace("–", "-").Replace("$", "");
            cleaned = Regex.Replace(cleaned, @"^\s*[\*\•]\s*", "", RegexOptions.Multiline);
            return cleaned.Trim();
        }
            public void LearnFromExample(string query, string solution)
        {
            SaveToHistory(query, solution);
        }

        public List<HistoryRecord> GetHistory()
        {
            return History ?? new List<HistoryRecord>();
        }
    }
}





