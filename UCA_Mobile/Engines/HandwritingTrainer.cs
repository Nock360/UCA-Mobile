using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;


namespace UCA_Mobile.Engines
{
    public static class HandwritingTrainer
    {
        public static readonly string GlyphsFolder = Path.GetFullPath("UserGlyphs");

        private static readonly Dictionary<char, char> Homoglyphs = new Dictionary<char, char>();

        static HandwritingTrainer()
        {
            Homoglyphs.Add('х', 'x'); Homoglyphs.Add('x', 'х');
            Homoglyphs.Add('с', 'c'); Homoglyphs.Add('c', 'с');
            Homoglyphs.Add('о', 'o'); Homoglyphs.Add('o', 'о');
            Homoglyphs.Add('у', 'y'); Homoglyphs.Add('y', 'у');
            Homoglyphs.Add('а', 'a'); Homoglyphs.Add('a', 'а');
            Homoglyphs.Add('е', 'e'); Homoglyphs.Add('e', 'е');
            Homoglyphs.Add('р', 'p'); Homoglyphs.Add('p', 'р');
            Homoglyphs.Add('к', 'k'); Homoglyphs.Add('k', 'к');
            Homoglyphs.Add('м', 'm'); Homoglyphs.Add('m', 'м');
            Homoglyphs.Add('т', 't'); Homoglyphs.Add('t', 'т');
        }

        public static int GetTrainedGlyphCount() =>
            Directory.Exists(GlyphsFolder) ? Directory.GetFiles(GlyphsFolder, "*.png").Length : 0;

        public static Bitmap? GetRandomGlyph(char c, Random rand)
        {
            if (!Directory.Exists(GlyphsFolder)) return null;
            var files = Directory.GetFiles(GlyphsFolder, $"u{(int)c:X4}_*.png");
            if (files.Length == 0) return null;
            using var fs = new FileStream(files.ElementAt(rand.Next(files.Length)), FileMode.Open, FileAccess.Read);
            return new Bitmap(fs);
        }

        public static void SaveSingleGlyph(char ch, Bitmap cleanGlyph)
        {
            Directory.CreateDirectory(GlyphsFolder);
            SaveCharFile(ch, cleanGlyph);
            if (Homoglyphs.TryGetValue(ch, out char pair))
                SaveCharFile(pair, cleanGlyph);
        }

        private static void SaveCharFile(char ch, Bitmap bmp)
        {
            string prefix = $"u{(int)ch:X4}_";
            int existing = Directory.GetFiles(GlyphsFolder, prefix + "*.png").Length;
            bmp.Save(Path.Combine(GlyphsFolder, $"{prefix}{existing + 1}.png"), ImageFormat.Png);
        }
        public static Bitmap? ExtractCleanInkGlyph(Bitmap source, Rectangle rect)
        {
            rect.Intersect(new Rectangle(0, 0, source.Width, source.Height));
            if (rect.Width < 3 || rect.Height < 3) return null;

            var lums = new List<double>();
            for (int y = 0; y < rect.Height; y++)
                for (int x = 0; x < rect.Width; x++)
                {
                    var p = source.GetPixel(rect.X + x, rect.Y + y);
                    lums.Add(0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
                }
            lums.Sort();
            double paperLum = lums.ElementAt((int)(lums.Count * 0.75));
            double cutoff = paperLum - 20.0;

            var outBmp = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int inkPixels = 0;

            for (int y = 0; y < rect.Height; y++)
                for (int x = 0; x < rect.Width; x++)
                {
                    var p = source.GetPixel(rect.X + x, rect.Y + y);
                    double lum = 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;
                    double diff = cutoff - lum;
                    if (diff > 0)
                    {
                        int alpha = (int)Math.Clamp(diff * 5.0 + 70, 80, 255);
                        outBmp.SetPixel(x, y, System.Drawing.Color.FromArgb(alpha, 20, 45, 130));
                        inkPixels++;
                    }
                    else
                    {
                        outBmp.SetPixel(x, y, System.Drawing.Color.Transparent);
                    }
                }

            if (inkPixels < 5) { outBmp.Dispose(); return null; }
            return outBmp;
        }

        public static async Task<int> TrainFromPhotoAsync(string imagePath)
        {
            Directory.CreateDirectory(GlyphsFolder);
            int learned = 0;
            using var srcBmp = new Bitmap(imagePath);
            int targetW = Math.Min(1800, Math.Max(1000, srcBmp.Width));
            int targetH = (int)(srcBmp.Height * ((double)targetW / srcBmp.Width));
            using var resized = new Bitmap(srcBmp, new System.Drawing.Size(targetW, targetH));

            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;
            var dec = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            var ocrResult = await OcrEngine.TryCreateFromUserProfileLanguages().RecognizeAsync(await dec.GetSoftwareBitmapAsync());

            foreach (var word in ocrResult.Lines.SelectMany(l => l.Words))
            {
                string text = word.Text.Trim();
                if (string.IsNullOrEmpty(text)) continue;
                var r = word.BoundingRect;
                double charW = r.Width / text.Length;
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text.ElementAt(i);
                    if (char.IsWhiteSpace(ch)) continue;
                    var rect = new Rectangle((int)(r.X + i * charW), (int)r.Y - 2, Math.Max(4, (int)Math.Ceiling(charW)), (int)r.Height + 4);
                    using var glyph = ExtractCleanInkGlyph(resized, rect);
                    if (glyph != null) { SaveSingleGlyph(ch, glyph); learned++; }
                }
            }
            return learned;
        }
        public static string CreateCalibrationSheet()
        {
            int w = 1240, h = 1754;
            using var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.White);
            using var titleFont = new Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
            using var sampleFont = new Font("Consolas", 22);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.LightGray, 1);

            g.DrawString("ШАБЛОН ПОЧЕРКА (UCA STUDIO)", titleFont, System.Drawing.Brushes.Black, 60, 50);
            var lines = new List<string>
            {
                "А Б В Г Д Е Ё Ж З И Й К Л М Н О П Р С Т У Ф Х Ц Ч Ш Щ Ъ Ы Ь Э Ю Я",
                "а б в г д е ё ж з и й к л м н о п р с т у ф х ц ч ш щ ъ ы ь э ю я",
                "A B C D E F G H I J K L M N O P Q R S T U V W X Y Z",
                "a b c d e f g h i j k l m n o p q r s t u v w x y z",
                "0 1 2 3 4 5 6 7 8 9  +  -  =  *  /  (  )  ^  .  ,  :"
            };
            int y = 140;
            foreach (var line in lines)
            {
                g.DrawString(line, sampleFont, System.Drawing.Brushes.Gray, 60, y);
                g.DrawLine(pen, 60, y + 90, w - 60, y + 90);
                y += 190;
            }
            string path = Path.GetFullPath("handwriting_template.png");
            bmp.Save(path, ImageFormat.Png);
            return path;
        }
    }

    public class GlyphCropperWindow : Window
    {
        private readonly Bitmap _sourceBmp;
        private readonly WrapPanel _cardsPanel = new() { Margin = new Thickness(10) };
        private readonly TextBlock _statusText;

        public GlyphCropperWindow(string imagePath)
        {
            Title = "Авто-извлечение почерка из фото — подпишите нужные символы";
            Width = 960; Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = UiBuilder.Hex(AppTheme.WindowBg);

            _sourceBmp = new Bitmap(imagePath);
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topBar = new Border { Background = UiBuilder.Hex(AppTheme.PanelBg), Padding = new Thickness(14, 10, 14, 10), BorderBrush = UiBuilder.Hex(AppTheme.BorderCol), BorderThickness = new Thickness(0, 0, 0, 1) };
            var hGrid = new Grid();
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusText = new TextBlock { Text = "Программа сама нарезала символы с фото. Введите букву/цифру под нужными картинками и нажмите Готово:", Foreground = UiBuilder.Hex(AppTheme.TextPrimary), FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var doneBtn = UiBuilder.CreateBtn("Сохранить и применить", true, (s, e) => Close(), 14, 6);
            hGrid.Children.Add(_statusText); Grid.SetColumn(doneBtn, 1); hGrid.Children.Add(doneBtn);
            topBar.Child = hGrid;
            root.Children.Add(topBar);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _cardsPanel };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);
            Content = root;

            Loaded += (s, e) => ExtractAllBlobs();
            Closed += (s, e) => _sourceBmp.Dispose();
        }

        private void ExtractAllBlobs()
        {
            int targetW = Math.Min(1400, _sourceBmp.Width);
            int targetH = (int)(_sourceBmp.Height * ((double)targetW / _sourceBmp.Width));
            using var bmp = new Bitmap(_sourceBmp, new System.Drawing.Size(targetW, targetH));

            bool[,] visited = new bool[targetW, targetH];
            var rects = new List<Rectangle>();

            for (int y = 5; y < targetH - 5; y += 2)
            {
                for (int x = 5; x < targetW - 5; x += 2)
                {
                    if (visited[x, y]) continue;
                    var p = bmp.GetPixel(x, y);
                    double lum = 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;
                    if (lum < 125 && (p.B - p.R) > -15)
                    {
                        var r = FloodFillBounds(bmp, visited, x, y, targetW, targetH);
                        if (r.Width >= 8 && r.Height >= 10 && r.Width <= 140 && r.Height <= 140)
                            rects.Add(r);
                    }
                }
            }

            foreach (var r in rects.Take(80))
            {
                var clean = HandwritingTrainer.ExtractCleanInkGlyph(bmp, r);
                if (clean == null) continue;

                using var ms = new MemoryStream();
                clean.Save(ms, ImageFormat.Png); ms.Position = 0;
                var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit();

                var card = new Border { Background = UiBuilder.Hex(AppTheme.PanelBg), BorderBrush = UiBuilder.Hex(AppTheme.BorderCol), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Margin = new Thickness(5), Width = 76 };
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var img = new System.Windows.Controls.Image { Source = bi, Width = 44, Height = 44, Stretch = Stretch.Uniform };
                var box = new TextBox { Width = 36, Height = 26, FontSize = 15, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0), MaxLength = 1 };

                var capturedBmp = new Bitmap(clean);
                box.TextChanged += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(box.Text))
                    {
                        HandwritingTrainer.SaveSingleGlyph(box.Text.First(), capturedBmp);
                        card.BorderBrush = System.Windows.Media.Brushes.SeaGreen;
                        card.BorderThickness = new Thickness(2);
                        _statusText.Text = $"В базе вашего почерка символов: {HandwritingTrainer.GetTrainedGlyphCount()}";
                    }
                };

                stack.Children.Add(img); stack.Children.Add(box);
                card.Child = stack;
                _cardsPanel.Children.Add(card);
                clean.Dispose();
            }
        }

        private Rectangle FloodFillBounds(Bitmap bmp, bool[,] visited, int startX, int startY, int w, int h)
        {
            int minX = startX, maxX = startX, minY = startY, maxY = startY;
            var q = new Queue<(int x, int y)>();
            q.Enqueue((startX, startY));
            visited[startX, startY] = true;
            int count = 0;

            while (q.Count > 0 && count < 4000)
            {
                var (cx, cy) = q.Dequeue();
                count++;
                if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;

                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            var p = bmp.GetPixel(nx, ny);
                            double lum = 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;
                            if (lum < 130 && (p.B - p.R) > -15) q.Enqueue((nx, ny));
                        }
                    }
            }
            return new Rectangle(Math.Max(0, minX - 2), Math.Max(0, minY - 2), Math.Min(w - minX, maxX - minX + 4), Math.Min(h - minY, maxY - minY + 4));
        }
    }
}



