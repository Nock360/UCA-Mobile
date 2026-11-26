using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Newtonsoft.Json;

namespace UCA_Mobile.Engines
{
    public static class VisionFormulaEngine
    {
                public static BitmapSource PreprocessNotebookPhoto(BitmapSource src)
        {
            var fmt = new FormatConvertedBitmap(src, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int w = fmt.PixelWidth, h = fmt.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[h * stride];
            fmt.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                // Отсекаем голубую/серую клетку тетради и тени, оставляя тёмные чернила
                int lum = (r * 30 + g * 59 + b * 11) / 100;
                bool isGridLine = (b > r + 15 && lum > 135);
                byte cleanVal = (lum < 145 && !isGridLine) ? (byte)0 : (byte)255;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = cleanVal;
                pixels[i + 3] = 255;
            }

            var wb = new WriteableBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            wb.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0);
            wb.Freeze();
            return wb;
        }

        public static async Task<(bool Success, string ExtractedFormula, string ModelUsed)> RecognizeHandwrittenMathAsync(BitmapSource imgSource, string ollamaUrl)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                string visionModel = "";

                try
                {
                    var tagsResp = await client.GetStringAsync("http://localhost:11434/api/tags");
                    dynamic? tagsObj = JsonConvert.DeserializeObject(tagsResp);
                    var models = new List<string>();
                    if (tagsObj?.models != null)
                        foreach (var item in tagsObj.models) models.Add((string)item.name);

                    visionModel = models.FirstOrDefault(m =>
                        m.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("llava", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("minicpm", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("vl", StringComparison.OrdinalIgnoreCase)) ?? "";
                }
                catch { }

                if (string.IsNullOrEmpty(visionModel))
                    return (false, "", "");

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(imgSource));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                string base64Image = Convert.ToBase64String(ms.ToArray());

                string prompt = "Ты — оптический распознаватель математических и физических формул. " +
                                "Перепиши формулу или условие задачи с этого изображения в одну чистую строку, понятную для калькулятора " +
                                "(используй стандартные символы: *, /, ^, sqrt(), int(f(x), a, b), sin, cos). " +
                                "НЕ решай задачу и НЕ пиши пояснений — выведи ТОЛЬКО саму распознанную формулу или текст задачи.";

                var payload = new
                {
                    model = visionModel,
                    prompt = prompt,
                    images = new[] { base64Image },
                    stream = false,
                    options = new { temperature = 0.1 }
                };

                var response = await client.PostAsync(ollamaUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
                dynamic? json = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                string rawOut = ((string)(json?.response ?? "")).Trim();
                string cleaned = SmartInputAndFormat.BeautifyMath(rawOut).Replace("\r", " ").Replace("\n", " ").Trim();

                return (!string.IsNullOrWhiteSpace(cleaned), cleaned, visionModel);
            }
            catch
            {
                return (false, "", "");
            }
        }
    }
}




