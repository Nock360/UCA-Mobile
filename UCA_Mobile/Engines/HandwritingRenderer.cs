using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace UCA_Mobile.Engines
{
    public enum PaperType { Grid, Lined, Blank }
    public enum InkColor { BlueBallpoint, PurpleInk, BlackGel }

    public static class HandwritingRenderer
    {
        private static readonly HashSet<char> Descenders = new("рРуУдДцЦщЩfFgGjJpPqQyY".ToCharArray());
        private static readonly HashSet<char> TallChars = new("0123456789()[]/|+=-*БВГДЕЁЖЗИЙКЛМНОПРСТФХЧШЪЫЬЭЮЯABCDEFGHIJKLMNOPQRSTUVWXYZбвёklhtdb".ToCharArray());

        public static List<string> RenderToNotebookPages(
            string solutionText,
            PaperType paper = PaperType.Grid,
            InkColor ink = InkColor.BlueBallpoint,
            bool addCameraPhotoEffect = true,
            List<Point>? graphPoints = null,
            string baseFileName = "notebook_page")
        {
            var generatedFiles = new List<string>();
            int width = 1240, height = 1754, cellSize = 34;
            var rand = new Random();

            Color inkRgb = ink switch
            {
                InkColor.PurpleInk => Color.FromRgb(68, 24, 125),
                InkColor.BlackGel => Color.FromRgb(25, 27, 33),
                _ => Color.FromRgb(24, 58, 158)
            };

            var typeface = new Typeface(new FontFamily("Segoe Print, Comic Sans MS"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
            var inkBrush = new SolidColorBrush(inkRgb);
            double startX = 68, lineHeight = cellSize * 2;
            int limitX = paper == PaperType.Blank ? width - 80 : width - 190;

            var allLines = solutionText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            int lineIndex = 0;
            int pageNum = 1;

            while (lineIndex < allLines.Count || (pageNum == 1))
            {
                var visual = new DrawingVisual();
                double currentY = 68;

                using (DrawingContext dc = visual.RenderOpen())
                {
                    DrawPaperBackground(dc, width, height, cellSize, paper, rand);

                    while (lineIndex < allLines.Count && currentY < height - 160)
                    {
                        string line = allLines.ElementAt(lineIndex);
                        double currentX = startX + rand.Next(-5, 6);
                        double lineSlope = (rand.NextDouble() - 0.5) * 0.008; // лёгкий естественный уклон всей строки
                        double wavePhase = rand.NextDouble() * Math.PI * 2;

                        for (int idx = 0; idx < line.Length; idx++)
                        {
                            char c = line.ElementAt(idx);
                            if (currentX > limitX)
                            {
                                currentX = startX + rand.Next(-4, 5);
                                currentY += lineHeight;
                                if (currentY >= height - 160) break;
                            }

                            if (c == ' ') { currentX += 18 + rand.Next(-3, 4); continue; }

                            double baselineShift = (currentX - startX) * lineSlope + Math.Sin(idx * 0.35 + wavePhase) * 1.8;

                            using var userGlyph = HandwritingTrainer.GetRandomGlyph(c, rand);
                            if (userGlyph != null)
                            {
                                bool isDesc = Descenders.Contains(c);
                                bool isTall = TallChars.Contains(c);

                                // Сохраняем естественные пропорции и высоту символов
                                double baseH = isTall ? 38.0 : (isDesc ? 42.0 : 28.0);
                                double targetH = baseH + rand.NextDouble() * 3.0 - 1.5;
                                double scale = targetH / userGlyph.Height;
                                double targetW = Math.Clamp(userGlyph.Width * scale, 9.0, 44.0);

                                double yPos = currentY + baselineShift + (isTall ? -6.0 : (isDesc ? 2.0 : 3.0));
                                double angle = rand.NextDouble() * 3.2 - 1.6;

                                var bmpSource = ConvertToTintedBitmapSource(userGlyph, inkRgb, rand);
                                dc.PushTransform(new RotateTransform(angle, currentX + targetW / 2, yPos + targetH / 2));
                                dc.DrawImage(bmpSource, new Rect(currentX, yPos, targetW, targetH));
                                dc.Pop();

                                // Плотное слитное расстояние между буквами в слове
                                bool nextIsSpace = (idx + 1 < line.Length && line.ElementAt(idx + 1) == ' ');
                                currentX += targetW * (nextIsSpace ? 0.85 : 0.72) + rand.NextDouble() * 1.5;
                            }
                            else
                            {
                                var fc = new FormattedText(c.ToString(), CultureInfo.GetCultureInfo("ru-ru"), FlowDirection.LeftToRight, typeface, 24 + rand.Next(-1, 2), inkBrush, 1.0);
                                double angle = -4 + (rand.NextDouble() * 5 - 2.5);
                                dc.PushTransform(new RotateTransform(angle, currentX, currentY + baselineShift));
                                dc.DrawText(fc, new Point(currentX, currentY + baselineShift));
                                dc.Pop();
                                currentX += fc.WidthIncludingTrailingWhitespace * 0.86;
                            }
                        }

                        currentY += lineHeight + rand.Next(-2, 3);
                        lineIndex++;
                    }
                    bool isTextDone = lineIndex >= allLines.Count;
                    if (isTextDone && graphPoints != null && graphPoints.Count > 2 && currentY < height - 380)
                    {
                        DrawGraphOnPaper(dc, inkBrush, inkRgb, currentY + 25, graphPoints);
                        graphPoints = null;
                    }

                    if (addCameraPhotoEffect)
                    {
                        var shadow = new RadialGradientBrush(Color.FromArgb(0, 255, 252, 240), Color.FromArgb(62, 18, 22, 34))
                        { GradientOrigin = new Point(0.32, 0.28), Center = new Point(0.48, 0.46), RadiusX = 0.88, RadiusY = 0.88 };
                        dc.DrawRectangle(shadow, null, new Rect(0, 0, width, height));
                    }
                }

                string filePath = Path.GetFullPath($"{baseFileName}_{pageNum}.png");
                SaveVisualToPng(visual, width, height, filePath);
                generatedFiles.Add(filePath);
                pageNum++;

                if (lineIndex >= allLines.Count && graphPoints != null && graphPoints.Count > 2)
                {
                    var gVisual = new DrawingVisual();
                    using (DrawingContext dc = gVisual.RenderOpen())
                    {
                        DrawPaperBackground(dc, width, height, cellSize, paper, rand);
                        DrawGraphOnPaper(dc, inkBrush, inkRgb, 100, graphPoints);
                    }
                    string gPath = Path.GetFullPath($"{baseFileName}_{pageNum}.png");
                    SaveVisualToPng(gVisual, width, height, gPath);
                    generatedFiles.Add(gPath);
                    break;
                }
            }

            return generatedFiles;
        }

        private static BitmapSource ConvertToTintedBitmapSource(System.Drawing.Bitmap bmp, Color tint, Random rand)
        {
            using var tinted = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            double pressureShift = 0.88 + rand.NextDouble() * 0.24; // легкая вариация нажима ручки на каждой букве
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    int alpha = (int)Math.Clamp(p.A * pressureShift, 0, 255);
                    tinted.SetPixel(x, y, System.Drawing.Color.FromArgb(alpha, tint.R, tint.G, tint.B));
                }

            using var ms = new MemoryStream();
            tinted.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        private static void DrawPaperBackground(DrawingContext dc, int width, int height, int cellSize, PaperType paper, Random rand)
        {
            // Теплый оттенок настоящей тетрадной бумаги
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(246, 244, 238)), null, new Rect(0, 0, width, height));

            // Легкое зерно бумаги
            var grainBrush = new SolidColorBrush(Color.FromArgb(14, 90, 85, 75));
            for (int i = 0; i < 1800; i++)
                dc.DrawRectangle(grainBrush, null, new Rect(rand.Next(width), rand.Next(height), 2, 2));

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(72, 115, 152, 205)), 1.0);
            if (paper == PaperType.Grid)
            {
                for (int x = 0; x < width; x += cellSize) dc.DrawLine(gridPen, new Point(x, 0), new Point(x, height));
                for (int y = 0; y < height; y += cellSize) dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
            }
            else if (paper == PaperType.Lined)
            {
                for (int y = cellSize * 2; y < height; y += cellSize) dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
            }

            if (paper != PaperType.Blank)
            {
                var marginPen = new Pen(new SolidColorBrush(Color.FromArgb(130, 215, 75, 75)), 2.0);
                dc.DrawLine(marginPen, new Point(width - 170, 0), new Point(width - 170, height));
            }
        }

        private static void DrawGraphOnPaper(DrawingContext dc, Brush inkBrush, Color inkRgb, double boxY, List<Point> graphPoints)
        {
            double boxX = 140, boxW = 440, boxH = 300;
            double centerX = boxX + boxW / 2, centerY = boxY + boxH / 2;
            var axisPen = new Pen(inkBrush, 2.0);
            dc.DrawLine(axisPen, new Point(boxX, centerY), new Point(boxX + boxW, centerY));
            dc.DrawLine(axisPen, new Point(centerX, boxY), new Point(centerX, boxY + boxH));
            dc.DrawLine(axisPen, new Point(boxX + boxW - 12, centerY - 6), new Point(boxX + boxW, centerY));
            dc.DrawLine(axisPen, new Point(boxX + boxW - 12, centerY + 6), new Point(boxX + boxW, centerY));
            dc.DrawLine(axisPen, new Point(centerX - 6, boxY + 12), new Point(centerX, boxY));
            dc.DrawLine(axisPen, new Point(centerX + 6, boxY + 12), new Point(centerX, boxY));

            var curvePen = new Pen(new SolidColorBrush(Color.FromArgb(225, inkRgb.R, inkRgb.G, inkRgb.B)), 2.5);
            for (int i = 1; i < graphPoints.Count; i++)
            {
                var p1 = graphPoints.ElementAt(i - 1);
                var p2 = graphPoints.ElementAt(i);
                if (double.IsNaN(p1.Y) || double.IsNaN(p2.Y)) continue;
                double sx1 = centerX + p1.X * 20, sy1 = centerY - p1.Y * 20;
                double sx2 = centerX + p2.X * 20, sy2 = centerY - p2.Y * 20;
                if (sx1 >= boxX && sx1 <= boxX + boxW && sy1 >= boxY && sy1 <= boxY + boxH &&
                    sx2 >= boxX && sx2 <= boxX + boxW && sy2 >= boxY && sy2 <= boxY + boxH)
                    dc.DrawLine(curvePen, new Point(sx1, sy1), new Point(sx2, sy2));
            }
        }

        private static void SaveVisualToPng(DrawingVisual visual, int width, int height, string path)
        {
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }
    }
}



