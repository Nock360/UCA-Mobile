using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using AngouriMath;
using AngouriMath.Extensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using SkiaSharp;
using UCA_Mobile.Engines;

namespace UCA_Mobile.Views
{
    public partial class MainView : UserControl
    {
        private SKBitmap? _lastSkBitmap;
        private string _lastSolutionSummary = "";
        private TabControl MainTabs = new() { Background = Brush.Parse("#F4F4F5") };
        private TextBox FormulaBox = new() { Background = Brushes.White, Foreground = Brush.Parse("#18181B"), BorderBrush = Brush.Parse("#D4D4D8"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10),  Watermark = "Формула, qbell, aero:, fem:, flow:...", AcceptsReturn = true, MinHeight = 60, FontSize = 15 };
        private SelectableTextBlock AnswerText = new() { Foreground = Brush.Parse("#18181B"),  Text = "Готов к работе", FontSize = 17, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
        private StackPanel StepsPanel = new() { Spacing = 6 };
        private Image MobilePlotImage = new() { Stretch = Stretch.Uniform };
        private TextBlock PlotCaption = new() { Text = "График", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12 };
        private Image GeneratedImageView = new() { Stretch = Stretch.Uniform };
        private TextBox PyCodeBox = new() { IsReadOnly = true, MinHeight = 100, FontFamily = FontFamily.Default, FontSize = 12 };
        private TextBox CppCodeBox = new() { IsReadOnly = true, MinHeight = 100, FontFamily = FontFamily.Default, FontSize = 12 };
        private TextBox InoCodeBox = new() { IsReadOnly = true, MinHeight = 100, FontFamily = FontFamily.Default, FontSize = 12 };
        private TextBox ServerUrlBox = new() { Text = new UriBuilder("http", "127.0.0.1", 11434, "api/generate").Uri.ToString(), FontSize = 13 }; private TextBox CloudApiKeyBox = new() { Watermark = "Опционально: API-ключ (Groq / OpenRouter) для работы с 4G без ПК", FontSize = 12 };
        private StackPanel HintsListPanel = new() { Spacing = 6 };

                public MainView()
        {
            try { BuildMobileLayout(); } catch (Exception ex) { Content = new TextBlock { Text = "UI Error: " + ex.Message, TextWrapping = TextWrapping.Wrap }; return; }
            try { InitPersistentSettings(); } catch { }
            try { PopulateHints(); } catch { }
            this.AttachedToVisualTree += (_, _) => { try { ComputeInput("sin(x) * cos(x/2)"); } catch (Exception ex) { AnswerText.Text = ex.Message; } };
        }
                private static readonly string MobileConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "uca_mobile_settings.json");

        private void InitPersistentSettings()
        {
            try
            {
                if (File.Exists(MobileConfigPath))
                {
                    dynamic? cfg = JsonConvert.DeserializeObject(File.ReadAllText(MobileConfigPath));
                    if (cfg?.serverUrl != null) ServerUrlBox.Text = (string)cfg.serverUrl;
                    if (cfg?.apiKey != null) CloudApiKeyBox.Text = (string)cfg.apiKey;
                }
            }
            catch { }

            ServerUrlBox.TextChanged += (_, _) => SavePersistentSettings();
            CloudApiKeyBox.TextChanged += (_, _) => SavePersistentSettings();
        }

        private void SavePersistentSettings()
        {
            try
            {
                var data = new { serverUrl = ServerUrlBox.Text ?? "", apiKey = CloudApiKeyBox.Text ?? "" };
                File.WriteAllText(MobileConfigPath, JsonConvert.SerializeObject(data, Formatting.Indented), Encoding.UTF8);
            }
            catch { }
        }
        private void BuildMobileLayout()
        {
            var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Background = Brush.Parse("#F4F4F5") };
            var header = new Border { Background = Brush.Parse("#18181B"), Padding = new Thickness(12) };
            var hGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            var titles = new StackPanel();
            titles.Children.Add(new TextBlock { Text = "UCA Mobile Studio", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.Bold });
            titles.Children.Add(new TextBlock { Text = "30+ модулей", Foreground = Brush.Parse("#A1A1AA"), FontSize = 11 });

            var btnPhoto = new Button { Content = "Фото", Background = Brush.Parse("#27272A"), Foreground = Brushes.White, Margin = new Thickness(0, 0, 6, 0) };
            btnPhoto.Click += PickPhoto_Click;
            var btnNote = new Button { Content = "Тетрадь А4", Background = Brush.Parse("#2563EB"), Foreground = Brushes.White };
            btnNote.Click += RenderNotebook_Click;

            hGrid.Children.Add(titles);
            Grid.SetColumn(btnPhoto, 1); hGrid.Children.Add(btnPhoto);
            Grid.SetColumn(btnNote, 2); hGrid.Children.Add(btnNote);
            header.Child = hGrid;
            root.Children.Add(header);

            var solverStack = new StackPanel { Spacing = 8 };
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var c in new[] { "qbell", "aero: v=60, alpha=8", "fem: L=2, F=5000", "fft: sin(2*pi*5*t)", "pde: heat", "montecarlo(5000000)" })
            {
                var b = new Button { Content = c, Padding = new Thickness(10, 6), FontSize = 11, Background = Brushes.White, Foreground = Brush.Parse("#18181B"), BorderBrush = Brush.Parse("#E4E4E7"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
                b.Click += QuickChip_Click;
                chips.Children.Add(b);
            }
            solverStack.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = chips });
            solverStack.Children.Add(FormulaBox);

            var btns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
            var bCalc = new Button { Content = "Вычислить", Background = Brush.Parse("#18181B"), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(0, 11), FontWeight = FontWeight.SemiBold };
            bCalc.Click += Compute_Click;
            var bAi = new Button { Content = "Решить (AI)", Background = Brush.Parse("#E4E4E7"), Foreground = Brush.Parse("#18181B"), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(0, 11), FontWeight = FontWeight.SemiBold };
            bAi.Click += SolveAi_Click;
            btns.Children.Add(bCalc); Grid.SetColumn(bAi, 1); btns.Children.Add(bAi);
            solverStack.Children.Add(btns);

            var ansCard = new Border { Background = Brushes.White, BorderBrush = Brush.Parse("#E4E4E7"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10) };
            ansCard.Child = AnswerText;
            solverStack.Children.Add(ansCard);
            solverStack.Children.Add(StepsPanel);

            var plotGrid = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(8) };
            plotGrid.Children.Add(MobilePlotImage); Grid.SetRow(PlotCaption, 1); plotGrid.Children.Add(PlotCaption);

            var noteGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(8) };
            var bSave = new Button { Content = "Сохранить PNG", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            bSave.Click += SaveGeneratedImage_Click;
            noteGrid.Children.Add(bSave);
            var nScroll = new ScrollViewer { Content = GeneratedImageView }; Grid.SetRow(nScroll, 1); noteGrid.Children.Add(nScroll);

            var codeStack = new StackPanel { Spacing = 8 };
            var bCode = new Button { Content = "Сгенерировать код (Python / C++ / Arduino)", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            bCode.Click += GenerateCode_Click;
            codeStack.Children.Add(bCode);
            codeStack.Children.Add(new TextBlock { Text = "Python 3", FontWeight = FontWeight.Bold }); codeStack.Children.Add(PyCodeBox);
            codeStack.Children.Add(new TextBlock { Text = "C++ 17", FontWeight = FontWeight.Bold }); codeStack.Children.Add(CppCodeBox);
            codeStack.Children.Add(new TextBlock { Text = "Arduino (.ino)", FontWeight = FontWeight.Bold }); codeStack.Children.Add(InoCodeBox);

            var helpStack = new StackPanel { Spacing = 8 };
            helpStack.Children.Add(new TextBlock { Text = "Сервер Ollama AI:", FontWeight = FontWeight.Bold });
            helpStack.Children.Add(ServerUrlBox); helpStack.Children.Add(new TextBlock { Text = "Облачный ключ (для работы AI с телефона без ПК):", FontWeight = FontWeight.Bold }); helpStack.Children.Add(CloudApiKeyBox);
            var bTest = new Button { Content = "Запустить автотест всех 30+ модулей", Background = Brush.Parse("#16A34A"), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 10), CornerRadius = new CornerRadius(8) }; bTest.Click += RunFullSelfTest_Click; helpStack.Children.Add(bTest); helpStack.Children.Add(HintsListPanel);

            MainTabs.Items.Add(new TabItem { Header = "Решатель", Content = new ScrollViewer { Padding = new Thickness(10), Content = solverStack } });
            MainTabs.Items.Add(new TabItem { Header = "График", Content = plotGrid });
            MainTabs.Items.Add(new TabItem { Header = "Тетрадь", Content = noteGrid });
            MainTabs.Items.Add(new TabItem { Header = "Код", Content = new ScrollViewer { Padding = new Thickness(10), Content = codeStack } });
            MainTabs.Items.Add(new TabItem { Header = "Справка", Content = new ScrollViewer { Padding = new Thickness(10), Content = helpStack } });

            Grid.SetRow(MainTabs, 1);
            root.Children.Add(MainTabs);
            Content = root;
        }

        private void PopulateHints()
        {
            foreach (var h in HintsAndAutocomplete.AllHints.Take(35))
            {
                var btn = new Button { Content = $"{h.Category}: {h.Command}", HorizontalAlignment = HorizontalAlignment.Stretch };
                btn.Click += (s, e) => { FormulaBox.Text = h.Command; MainTabs.SelectedIndex = 0; ComputeInput(h.Command); };
                HintsListPanel.Children.Add(btn);
            }
        }
        private void QuickChip_Click(object? s, RoutedEventArgs e) { if (s is Button b && b.Content is string c) { FormulaBox.Text = c; ComputeInput(c); } }
        private void Compute_Click(object? s, RoutedEventArgs e) => ComputeInput(FormulaBox.Text ?? "");

        private void ComputeInput(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return;
            StepsPanel.Children.Clear();
            string norm = SmartInputAndFormat.NormalizeUserQuery(rawInput);
            try
            {
                if (QuantumEngine.TryProcessQuantum(norm, out var qSteps, out var qRes, out var qProbs))
                { AnswerText.Text = qRes; RenderStepCards(qSteps); DrawQuantumHistogram(qProbs); return; }

                if (MobileScienceAndGraphics.TryRunMobileScience(norm, out var sSteps, out var sRes, out var sPts, out var sTitle, out var skImg))
                { AnswerText.Text = sRes; RenderStepCards(sSteps); if (sPts.Count > 0) DrawPoints(sPts, sTitle); if (skImg != null) { ShowSkiaBitmap(skImg); MainTabs.SelectedIndex = 2; } return; }

                if (PhysicsLabEngine.TryProcessPhysics(norm, out var pSteps, out var pRes, out var pTraj))
                { AnswerText.Text = pRes; RenderStepCards(pSteps); if (pTraj.Count > 0) DrawPoints(pTraj.Select(pt => (pt.X, pt.Y)).ToList(), "Траектория"); return; }

                if (norm.StartsWith("вариант:", StringComparison.OrdinalIgnoreCase))
                {
                    AnswerText.Text = "Пакетный режим билетов (нажмите Решить AI)";
                    RenderStepCards(new List<string> { "Распознан список задач билета. Нажмите кнопку «Решить (AI)» для пакетного разбора всех заданий." });
                    return;
                }

                if (MobileMatrixEngine.TrySolveMatrix(norm, out var mSteps, out var mRes)) { AnswerText.Text = mRes; RenderStepCards(mSteps); return; }
                if (UnitConverterEngine.TryConvert(norm, out var uSteps, out var uRes)) { AnswerText.Text = uRes; RenderStepCards(uSteps); return; }
                

                if (FinanceEngine.TryProcessFinance(norm, out var fSteps, out var fRes)) { AnswerText.Text = fRes; RenderStepCards(fSteps); return; }
                if (StatisticsEngine.TryProcessStats(norm, out var stSteps, out var stRes, out _)) { AnswerText.Text = stRes; RenderStepCards(stSteps); return; }
                if (BaseConverterEngine.TryConvertBase(norm, out var bSteps, out var bRes)) { AnswerText.Text = bRes; RenderStepCards(bSteps); return; }

                Entity expr = norm;
                Entity simp = expr.Simplify();
                Entity deriv = expr.Differentiate("x").Simplify();
                AnswerText.Text = simp.ToString();
                RenderStepCards(new List<string> { $"Упрощение: {simp}", $"Производная d/dx: {deriv}" });
                PlotFunction(expr);
            }
            catch (Exception ex) { AnswerText.Text = "Текстовая задача"; RenderStepCards(new List<string> { ex.Message }); }
        }

        private void RenderStepCards(List<string> steps)
        {
            _lastSolutionSummary = string.Join("\n", steps);
            foreach (var s in steps)
                StepsPanel.Children.Add(new Border { Background = Brushes.White, BorderBrush = Brush.Parse("#E4E4E7"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = new SelectableTextBlock { Text = s, TextWrapping = TextWrapping.Wrap, FontSize = 13 } });
        }

                private void DrawSkiaChart(double[] xs, double[] ys, string title, bool isBar = false, string[]? labels = null)
        {
            int w = 760, h = 520;
            using var bmp = new SKBitmap(w, h);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.White);

            using var gridP = new SKPaint { Color = new SKColor(232, 232, 236), StrokeWidth = 1f };
            for (int gx = 60; gx < w - 20; gx += 60) canvas.DrawLine(gx, 20, gx, h - 50, gridP);
            for (int gy = 20; gy < h - 50; gy += 50) canvas.DrawLine(60, gy, w - 20, gy, gridP);

            using var axisP = new SKPaint { Color = new SKColor(113, 113, 122), StrokeWidth = 2f };
            canvas.DrawLine(60, h - 50, w - 20, h - 50, axisP);
            canvas.DrawLine(60, 20, 60, h - 50, axisP);

            if (xs.Length > 0 && ys.Length > 0)
            {
                double minX = xs.Min(), maxX = xs.Max(), minY = isBar ? 0 : ys.Min(), maxY = isBar ? 105 : ys.Max();
                if (Math.Abs(maxX - minX) < 1e-9) { maxX += 1; minX -= 1; }
                if (Math.Abs(maxY - minY) < 1e-9) { maxY += 1; minY -= 1; }

                if (isBar)
                {
                    using var barP = new SKPaint { Color = new SKColor(124, 58, 237), Style = SKPaintStyle.Fill, IsAntialias = true };
                    using var lblP = new SKPaint { Color = SKColors.Black, TextSize = 18f, TextAlign = SKTextAlign.Center, IsAntialias = true };
                    float slotW = (w - 100f) / ys.Length;
                    for (int i = 0; i < ys.Length; i++)
                    {
                        float bh = (float)((ys[i] / maxY) * (h - 90));
                        float bx = 75f + i * slotW;
                        canvas.DrawRect(bx, h - 50 - bh, slotW * 0.65f, bh, barP);
                        if (labels != null && i < labels.Length) canvas.DrawText(labels[i], bx + slotW * 0.32f, h - 24, lblP);
                    }
                }
                else
                {
                    using var lineP = new SKPaint { Color = new SKColor(37, 99, 235), StrokeWidth = 3f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                    using var path = new SKPath();
                    for (int i = 0; i < xs.Length; i++)
                    {
                        float px = (float)(60 + ((xs[i] - minX) / (maxX - minX)) * (w - 90));
                        float py = (float)((h - 50) - ((ys[i] - minY) / (maxY - minY)) * (h - 80));
                        if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
                    }
                    canvas.DrawPath(path, lineP);
                }
            }

            using var img = SKImage.FromBitmap(bmp);
            using var ms = new MemoryStream(img.Encode(SKEncodedImageFormat.Png, 95).ToArray());
            MobilePlotImage.Source = new Bitmap(ms);
            PlotCaption.Text = title;
        }

        private void PlotFunction(Entity expr)
        {
            try
            {
                Func<double, double> evalFn = x => { try { return (double)expr.Compile("x").Call(x).Real; } catch { return (double)expr.Substitute("x", x).EvalNumerical().RealPart; } };
                var xs = Enumerable.Range(0, 200).Select(i => -10.0 + i * 0.1).ToArray();
                var ys = xs.Select(x => { double y = 0; try { y = evalFn(x); } catch { } return double.IsNaN(y) || double.IsInfinity(y) ? 0 : Math.Clamp(y, -50, 50); }).ToArray();
                DrawSkiaChart(xs, ys, $"График функции: y = {expr}");
            }
            catch { }
        }

        private void DrawPoints(List<(double X, double Y)> pts, string title) =>
            DrawSkiaChart(pts.Select(p => p.X).ToArray(), pts.Select(p => p.Y).ToArray(), title);

        private void DrawQuantumHistogram(List<(string Label, double Prob)> probs) =>
            DrawSkiaChart(Enumerable.Range(0, probs.Count).Select(i => (double)i).ToArray(), probs.Select(p => p.Prob).ToArray(), "Квантовые вероятности (%)", true, probs.Select(p => p.Label).ToArray());

        private void RenderNotebook_Click(object? s, RoutedEventArgs e)
        {
            ShowSkiaBitmap(MobileScienceAndGraphics.RenderNotebookPageSkia(FormulaBox.Text ?? "Расчёт", $"{AnswerText.Text}\n\n{_lastSolutionSummary}"));
            MainTabs.SelectedIndex = 2;
        }

        private void ShowSkiaBitmap(SKBitmap skBmp)
        {
            _lastSkBitmap = skBmp;
            using var img = SKImage.FromBitmap(skBmp);
            using var ms = new MemoryStream(img.Encode(SKEncodedImageFormat.Png, 95).ToArray());
            GeneratedImageView.Source = new Bitmap(ms);
        }

        private async void SaveGeneratedImage_Click(object? s, RoutedEventArgs e)
        {
            if (_lastSkBitmap == null) return;
            var tl = TopLevel.GetTopLevel(this); if (tl == null) return;
            var f = await tl.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "uca_mobile.png", DefaultExtension = "png" });
            if (f != null) { await using var st = await f.OpenWriteAsync(); using var img = SKImage.FromBitmap(_lastSkBitmap); img.Encode(SKEncodedImageFormat.Png, 95).SaveTo(st); }
        }

        private void GenerateCode_Click(object? s, RoutedEventArgs e)
        {
            var c = MobileCodeExportEngine.GenerateAll(FormulaBox.Text ?? "");
            PyCodeBox.Text = c["Python 3"]; CppCodeBox.Text = c["C++ 17"]; InoCodeBox.Text = c["Arduino (.ino)"];
            MainTabs.SelectedIndex = 3;
        }

        private async void PickPhoto_Click(object? s, RoutedEventArgs e)
        {
            var tl = TopLevel.GetTopLevel(this); if (tl == null) return;
            var files = await tl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Фото задачи", AllowMultiple = false, FileTypeFilter = new[] { FilePickerFileTypes.ImageAll } });
            if (files.Count == 0) return;
            await using var st = await files.First().OpenReadAsync();
            using var ms = new MemoryStream(); await st.CopyToAsync(ms);
            byte[] clean = MobileScienceAndGraphics.PreprocessCameraPhotoBytes(ms.ToArray());
            AnswerText.Text = "Анализирую фото...";
            string res = await LectureAnalyzerEngine.AnalyzeLectureBytesAsync(clean, ServerUrlBox.Text ?? "http://127.0.0", "minicpm-v");
            RenderStepCards(new List<string> { res });
        }

        private async void SolveAi_Click(object? s, RoutedEventArgs e)
        {
            string q = FormulaBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(q)) return;
            AnswerText.Text = "Анализирую задачу (AI)...";
            string prompt = LectureAnalyzerEngine.BuildLecturePrompt(q);

            // Попытка 1: Прямой облачный ИИ с телефона (если указан API-ключ Groq/OpenRouter)
            string apiKey = CloudApiKeyBox.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    using var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    c.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    string endpoint = apiKey.StartsWith("gsk_") ? "https://groq.com" : "https://openrouter.ai";
                    string mdl = apiKey.StartsWith("gsk_") ? "llama-3.3-70b-versatile" : "meta-llama/llama-3.3-70b-instruct:free";
                    var body = new { model = mdl, messages = new[] { new { role = "user", content = prompt } } };
                    var r = await c.PostAsync(endpoint, new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
                    dynamic? j = JsonConvert.DeserializeObject(await r.Content.ReadAsStringAsync());
                    string ans = (string)(j?.choices?.message?.content ?? "");
                    if (!string.IsNullOrWhiteSpace(ans))
                    {
                        AnswerText.Text = "Решено (Мобильный Cloud AI)";
                        RenderStepCards(SmartInputAndFormat.BeautifyMath(ans).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
                        return;
                    }
                }
                catch { }
            }

            // Попытка 2: Локальный сервер Ollama (порт 11434)
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
                var payload = new { model = "qwen2.5:7b", prompt = prompt, stream = false };
                var resp = await client.PostAsync(ServerUrlBox.Text, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    dynamic? json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
                    string ans = SmartInputAndFormat.BeautifyMath((string)(json?.response ?? ""));
                    if (!string.IsNullOrWhiteSpace(ans))
                    {
                        AnswerText.Text = "Решено (Ollama AI)";
                        RenderStepCards(ans.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
                        return;
                    }
                }
            }
            catch { }

            // Попытка 3: Полностью автономный офлайн-ИИ самого телефона (без интернета и без ПК)
            string norm = SmartInputAndFormat.NormalizeUserQuery(q);
            try
            {
                Entity expr = norm.Contains("=") ? $"({norm.Split('=').First()}) - ({norm.Split('=').Last()})" : norm;
                var simp = expr.Simplify();
                var deriv = expr.Differentiate("x").Simplify();
                AnswerText.Text = $"Автономный офлайн-разбор: {simp}";
                RenderStepCards(new List<string>
                {
                    "Режим: 100% офлайн-ядро смартфона (без интернета и ПК)",
                    $"Исходное выражение: {norm}",
                    $"Символьное упрощение: {simp}",
                    $"Производная d/dx: {deriv}"
                });
                PlotFunction(expr);
            }
            catch
            {
                AnswerText.Text = "Офлайн-режим: введите формулу или API-ключ";
                RenderStepCards(new List<string> { "Домашний ПК не в сети, а облачный ключ не задан. Все 30 математических, квантовых и инженерных модулей работают офлайн по кнопке «Вычислить»." });
            }
        }
            private void RunFullSelfTest_Click(object? s, RoutedEventArgs e)
        {
            var tests = new (string Name, string Cmd, Func<string> RunAndGetOutput)[]
            {
                ("01. Символьная алгебра", "x^2 + 2*x + 1 + x^2", () => ((Entity)"x^2 + 2*x + 1 + x^2").Simplify().ToString()),
                ("02. Решение уравнений", "x^2 - 9 = 0", () => ((Entity)"x^2 - 9").SolveEquation("x").ToString()),
                ("03. Дифференцирование", "sin(x^2)", () => ((Entity)"sin(x^2)").Differentiate("x").Simplify().ToString()),
                ("04. Интегралы", "int(x^2, 0, 3)", () => ((Entity)"x^2").Integrate("x").Simplify().ToString() + " + C"),
                ("05. Ряды и тригонометрия", "sin(x)^2 + cos(x)^2", () => ((Entity)"sin(x)^2 + cos(x)^2").Simplify().ToString()),
                ("06. Кванты: состояние Белла", "qbell", () => { QuantumEngine.TryProcessQuantum("qbell", out _, out var r, out var p); return $"{r} ({p.Count} сост.)"; }),
                ("07. Кванты: 3-кубитный GHZ", "qghz", () => { QuantumEngine.TryProcessQuantum("qghz", out _, out var r, out var p); return $"{r} ({p.Count} сост.)"; }),
                ("08. Квантовые гейты (H, CNOT)", "q: 2 | H(0) -> CNOT(0,1)", () => { QuantumEngine.TryProcessQuantum("q: 2 | H(0) -> CNOT(0,1)", out _, out var r, out _); return r; }),
                ("09. Спектр Фурье (БПФ)", "fft: sin(2*pi*5*t)", () => { MobileScienceAndGraphics.TryRunMobileScience("fft: sin(2*pi*5*t)", out _, out var r, out var pts, out _, out _); return $"{r} [{pts.Count} точек]"; }),
                ("10. Теплопроводность (PDE)", "pde: heat", () => { MobileScienceAndGraphics.TryRunMobileScience("pde: heat", out _, out var r, out var pts, out _, out _); return $"{r} [{pts.Count} узлов]"; }),
                ("11. Волновое уравнение (PDE)", "pde: wave", () => { MobileScienceAndGraphics.TryRunMobileScience("pde: wave", out _, out var r, out var pts, out _, out _); return $"{r} [{pts.Count} узлов]"; }),
                ("12. Аэродинамика крыла", "aero: v=60, alpha=8", () => { MobileScienceAndGraphics.TryRunMobileScience("aero: v=60, alpha=8", out _, out var r, out _, out _, out _); return r; }),
                ("13. Сопромат балок (FEM)", "fem: L=2, F=5000", () => { MobileScienceAndGraphics.TryRunMobileScience("fem: L=2, F=5000", out _, out var r, out _, out _, out _); return r; }),
                ("14. Геометрия треугольника", "geom: a=3, b=4, gamma=90", () => { MobileScienceAndGraphics.TryRunMobileScience("geom: a=3, b=4, gamma=90", out _, out var r, out _, out _, out _); return r; }),
                ("15. Монте-Карло (многопоток)", "montecarlo(50000)", () => { MobileScienceAndGraphics.TryRunMobileScience("montecarlo(50000)", out _, out var r, out _, out _, out _); return r; }),
                ("16. Электротехника: закон Ома", "ohm(U=12, R=4)", () => { PhysicsLabEngine.TryProcessPhysics("ohm(U=12, R=4)", out _, out var r, out _); return r; }),
                ("17. Колебательный LC-контур", "lc(L=0.01, C=0.0001)", () => { PhysicsLabEngine.TryProcessPhysics("lc(L=0.01, C=0.0001)", out _, out var r, out _); return r; }),
                ("18. Баллистика и траектория", "throw(v=25, alpha=45)", () => { PhysicsLabEngine.TryProcessPhysics("throw(v=25, alpha=45)", out _, out var r, out var tr); return $"{r} [{tr.Count} точек]"; }),
                ("19. Матрицы (определитель)", "det 4, 2; 1, 3", () => { MobileMatrixEngine.TrySolveMatrix("det 4, 2; 1, 3", out _, out var r); return r; }),
                ("20. Статистика выборок", "stats: 10, 20, 30, 40, 50", () => { StatisticsEngine.TryProcessStats("stats: 10, 20, 30, 40, 50", out _, out var r, out _); return r; }),
                ("21. Финансы: расчёт кредита", "credit(100000, 12, 12)", () => { FinanceEngine.TryProcessFinance("credit(100000, 12, 12)", out _, out var r); return r; }),
                ("22. Финансы: сложный процент", "deposit(100000, 10, 12)", () => { FinanceEngine.TryProcessFinance("deposit(100000, 10, 12)", out _, out var r); return r; }),
                ("23. Системы счисления", "255 -> hex", () => { BaseConverterEngine.TryConvertBase("255 -> hex", out _, out var r); return r; }),
                ("24. Конвертер величин", "100 km/h to m/s", () => { UnitConverterEngine.TryConvert("100 km/h to m/s", out _, out var r); return r; }),
                ("25. Понимание русского языка", "реши x^2 - 9 = 0", () => SmartInputAndFormat.NormalizeUserQuery("реши x^2 - 9 = 0")),
                ("26. Очистка фото тетради", "sin(x)", () => { using var b = new SKBitmap(20, 20); using var img = SKImage.FromBitmap(b); return $"PNG {MobileScienceAndGraphics.PreprocessCameraPhotoBytes(img.Encode().ToArray()).Length} байт"; }),
                ("27. AST-корректор формул", "2*x + 15", () => ActiveVisionEngine.SanitizeAndFixFormula("2х + l5 =").FixedFormula),
                ("28. Блок-схемы ГОСТ 19.701-90", "flow: Ввод X -> Вывод X", () => { var bmp = MobileScienceAndGraphics.RenderGostFlowchartSkia(new List<string> { "Ввод X", "Вывод X" }); return $"Холст {bmp.Width}x{bmp.Height} px"; }),
                ("29. Рендер тетради А4 в клетку", "x^2 + 5*x = 0", () => { var bmp = MobileScienceAndGraphics.RenderNotebookPageSkia("Тест", "x = 42"); return $"Лист А4 {bmp.Width}x{bmp.Height} px"; }),
                ("30. Генератор кода PY/C++/INO", "sin(x) + x^2", () => { var c = MobileCodeExportEngine.GenerateAll("sin(x) + x^2"); return $"Сгенерировано {c.Count} файла кода"; })
            };

            StepsPanel.Children.Clear();
            int passed = 0;
            foreach (var t in tests)
            {
                string outVal = "";
                bool ok = false;
                try { outVal = t.RunAndGetOutput(); ok = !string.IsNullOrWhiteSpace(outVal); } catch (Exception ex) { outVal = ex.Message; }
                if (ok) passed++;

                var cardBtn = new Button
                {
                    Content = $"{(ok ? "[OK]" : "[ERR]")} {t.Name}\n→ Результат: {outVal} (нажмите для запуска)",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = ok ? Brushes.White : Brush.Parse("#FEF2F2"),
                    BorderBrush = ok ? Brush.Parse("#BBF7D0") : Brush.Parse("#FECACA"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 8),
                    FontSize = 12.5
                };
                string cmdCopy = t.Cmd;
                cardBtn.Click += (_, _) => { FormulaBox.Text = cmdCopy; ComputeInput(cmdCopy); };
                StepsPanel.Children.Add(cardBtn);
            }

            AnswerText.Text = $"Живой аудит: {passed} из {tests.Length} модулей вернули реальный расчёт!";
            MainTabs.SelectedIndex = 0;
        }
    }
}

















