using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace UCA_Mobile.Engines
{
    public class LectureMethodProfile
    {
        public string Topic { get; set; } = "";
        public string RawNotes { get; set; } = "";
        public string ExtractedRules { get; set; } = "";
    }

    public static class LectureAnalyzerEngine
    {
        private static readonly string ProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "uca_lecture_method.json");
        public static LectureMethodProfile CurrentMethod { get; private set; } = LoadMethod();

        private static LectureMethodProfile LoadMethod()
        {
            try { if (File.Exists(ProfilePath)) return JsonConvert.DeserializeObject<LectureMethodProfile>(File.ReadAllText(ProfilePath)) ?? new(); } catch { }
            return new LectureMethodProfile();
        }

        public static void SaveMethod(LectureMethodProfile profile)
        {
            CurrentMethod = profile;
            try { File.WriteAllText(ProfilePath, JsonConvert.SerializeObject(profile, Formatting.Indented), Encoding.UTF8); } catch { }
        }

        public static void ClearMethod() { CurrentMethod = new LectureMethodProfile(); if (File.Exists(ProfilePath)) File.Delete(ProfilePath); }

        public static async Task<string> AnalyzeLectureBytesAsync(byte[] imageBytes, string aiUrl, string modelName)
        {
            string b64 = Convert.ToBase64String(imageBytes);
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            string prompt = "Изучи фото лекции/конспекта преподавателя. Выдели используемые обозначения переменных, формулы и стиль оформления решения.";
            var payload = new { model = modelName, prompt = prompt, images = new[] { b64 }, stream = false };
            var resp = await client.PostAsync(aiUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
            dynamic? json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
            string rules = (string)(json?.response ?? "Методика сохранена");
            SaveMethod(new LectureMethodProfile { Topic = "Конспект с телефона", ExtractedRules = rules });
            return rules;
        }

        public static string ApplyLectureStyleToPrompt(string taskText, string? extra = null) => BuildLecturePrompt(taskText); public static string BuildLecturePrompt(string taskText)
        {
            if (string.IsNullOrWhiteSpace(CurrentMethod.ExtractedRules)) return taskText;
            return $"Реши задачу строго по методике и обозначениям из конспекта преподавателя:\n{CurrentMethod.ExtractedRules}\n\nЗадача: {taskText}";
        }
    }
}


