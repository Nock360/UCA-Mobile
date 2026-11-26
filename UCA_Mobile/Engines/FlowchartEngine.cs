using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace UCA_Mobile.Engines
{
    public enum FlowNodeType { StartEnd, Process, Decision, InputOutput, Loop }
    public record FlowNode(FlowNodeType Type, string Text, string? AltBranch = null);

    public static class FlowchartEngine
    {
        public static bool TryBuildFlowchart(string input, out List<string> steps, out string result, out string imagePath)
        {
            steps = new List<string>();
            result = "";
            imagePath = "";

            string clean = input.Trim();
            if (!clean.StartsWith("flow:", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("блок-схема:", StringComparison.OrdinalIgnoreCase))
                return false;

            string body = clean.Substring(clean.IndexOf(':') + 1).Trim();
            var nodes = ParseAlgorithm(body);
            if (nodes.Count < 2) return false;

            imagePath = RenderGostFlowchart(nodes);
            result = $"Блок-схема ГОСТ 19.701-90 ({nodes.Count} блоков)";
            steps.Add("Построена расширенная блок-схема по ГОСТ с ветвлениями и циклами:");
            foreach (var n in nodes) steps.Add($"[{n.Type}] {n.Text}" + (n.AltBranch != null ? $" (Иначе: {n.AltBranch})" : ""));
            steps.Add($"Сохранено в файл: {Path.GetFileName(imagePath)}");
            return true;
        }

        private static List<FlowNode> ParseAlgorithm(string body)
        {
            var nodes = new List<FlowNode> { new(FlowNodeType.StartEnd, "Начало") };
            var parts = body.Contains("->")
                ? body.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : body.Split(new[] { "\r\n", "\n", ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = 0; i < parts.Count; i++)
            {
                string s = parts.ElementAt(i).Trim();
                if (string.IsNullOrWhiteSpace(s) || s == "{" || s == "}") continue;

                if (Regex.IsMatch(s, @"^(while|пока|for|для)\b", RegexOptions.IgnoreCase))
                    nodes.Add(new FlowNode(FlowNodeType.Loop, s.Replace("{", "").Trim()));
                else if (Regex.IsMatch(s, @"^(if|если)\b", RegexOptions.IgnoreCase))
                {
                    string? elseText = null;
                    if (i + 2 < parts.Count && Regex.IsMatch(parts.ElementAt(i + 2).Trim(), @"^(else|иначе)\b", RegexOptions.IgnoreCase))
                    {
                        elseText = Regex.Replace(parts.ElementAt(i + 2).Trim(), @"^(else|иначе)\s*:?\s*", "", RegexOptions.IgnoreCase);
                        parts.RemoveAt(i + 2);
                    }
                    nodes.Add(new FlowNode(FlowNodeType.Decision, s.Replace("{", "").Trim(), elseText));
                }
                else if (Regex.IsMatch(s, @"^(ввод|вывод|input|print|Console\.Write|return|вернуть)\b", RegexOptions.IgnoreCase))
                    nodes.Add(new FlowNode(FlowNodeType.InputOutput, s));
                else
                    nodes.Add(new FlowNode(FlowNodeType.Process, s));
            }

            nodes.Add(new FlowNode(FlowNodeType.StartEnd, "Конец"));
            return nodes;
        }
        private static string RenderGostFlowchart(List<FlowNode> nodes)
        {
            int width = 920, stepY = 125;
            int height = Math.Max(460, nodes.Count * stepY + 90);
            var visual = new DrawingVisual();

            var bgBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            var strokePen = new Pen(new SolidColorBrush(Color.FromRgb(24, 24, 27)), 2.0);
            var branchPen = new Pen(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 1.8);
            var fillBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            var decisionBrush = new SolidColorBrush(Color.FromRgb(239, 246, 255));
            var loopBrush = new SolidColorBrush(Color.FromRgb(254, 249, 195));
            var textBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
            var tf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            var ru = System.Globalization.CultureInfo.GetCultureInfo("ru-ru");

            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));
                double centerX = 340, y = 40, boxW = 260, boxH = 62;

                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes.ElementAt(i);
                    double left = centerX - boxW / 2.0;

                    if (node.Type == FlowNodeType.StartEnd)
                        dc.DrawRoundedRectangle(fillBrush, strokePen, new Rect(left + 20, y, boxW - 40, boxH - 10), 26, 26);
                    else if (node.Type == FlowNodeType.Decision || node.Type == FlowNodeType.Loop)
                    {
                        var geom = new StreamGeometry();
                        using (var ctx = geom.Open())
                        {
                            ctx.BeginFigure(new Point(centerX, y - 8), true, true);
                            ctx.LineTo(new Point(left + boxW + 20, y + boxH / 2.0), true, false);
                            ctx.LineTo(new Point(centerX, y + boxH + 8), true, false);
                            ctx.LineTo(new Point(left - 20, y + boxH / 2.0), true, false);
                        }
                        dc.DrawGeometry(node.Type == FlowNodeType.Loop ? loopBrush : decisionBrush, strokePen, geom);
                    }
                    else if (node.Type == FlowNodeType.InputOutput)
                    {
                        var geom = new StreamGeometry();
                        using (var ctx = geom.Open())
                        {
                            ctx.BeginFigure(new Point(left + 22, y), true, true);
                            ctx.LineTo(new Point(left + boxW, y), true, false);
                            ctx.LineTo(new Point(left + boxW - 22, y + boxH), true, false);
                            ctx.LineTo(new Point(left, y + boxH), true, false);
                        }
                        dc.DrawGeometry(fillBrush, strokePen, geom);
                    }
                    else
                        dc.DrawRectangle(fillBrush, strokePen, new Rect(left, y, boxW, boxH));

                    var ft = new FormattedText(node.Text, ru, FlowDirection.LeftToRight, tf, 12.5, textBrush, 1.0) { MaxTextWidth = boxW - 36, TextAlignment = TextAlignment.Center };
                    dc.DrawText(ft, new Point(left + 18, y + (boxH - ft.Height) / 2.0));

                    if (node.Type == FlowNodeType.Decision)
                    {
                        double rightTip = left + boxW + 20, midY = y + boxH / 2.0, altX = 660;
                        dc.DrawLine(branchPen, new Point(rightTip, midY), new Point(altX, midY));
                        dc.DrawText(new FormattedText("Нет", ru, FlowDirection.LeftToRight, tf, 11, Brushes.Crimson, 1.0), new Point(rightTip + 8, midY - 16));

                        if (!string.IsNullOrEmpty(node.AltBranch))
                        {
                            dc.DrawRectangle(fillBrush, branchPen, new Rect(altX - 90, midY - 24, 180, 48));
                            var altFt = new FormattedText(node.AltBranch, ru, FlowDirection.LeftToRight, tf, 11.5, textBrush, 1.0) { MaxTextWidth = 165, TextAlignment = TextAlignment.Center };
                            dc.DrawText(altFt, new Point(altX - 82, midY - altFt.Height / 2.0));
                        }
                        dc.DrawLine(branchPen, new Point(altX, midY + (string.IsNullOrEmpty(node.AltBranch) ? 0 : 24)), new Point(altX, y + stepY + 25));
                        dc.DrawLine(branchPen, new Point(altX, y + stepY + 25), new Point(centerX + boxW / 2.0, y + stepY + 25));
                    }

                    if (node.Type == FlowNodeType.Loop && i + 1 < nodes.Count - 1)
                    {
                        double loopLeft = 110, nextMidY = y + stepY + boxH / 2.0, curMidY = y + boxH / 2.0;
                        dc.DrawLine(branchPen, new Point(left, nextMidY), new Point(loopLeft, nextMidY));
                        dc.DrawLine(branchPen, new Point(loopLeft, nextMidY), new Point(loopLeft, curMidY));
                        dc.DrawLine(branchPen, new Point(loopLeft, curMidY), new Point(left - 20, curMidY));
                    }

                    if (i < nodes.Count - 1)
                    {
                        double y1 = y + boxH + ((node.Type == FlowNodeType.Decision || node.Type == FlowNodeType.Loop) ? 8 : 0);
                        double y2 = y + stepY;
                        dc.DrawLine(strokePen, new Point(centerX, y1), new Point(centerX, y2));
                        dc.DrawLine(strokePen, new Point(centerX - 5, y2 - 8), new Point(centerX, y2));
                        dc.DrawLine(strokePen, new Point(centerX + 5, y2 - 8), new Point(centerX, y2));
                        if (node.Type == FlowNodeType.Decision || node.Type == FlowNodeType.Loop)
                            dc.DrawText(new FormattedText("Да", ru, FlowDirection.LeftToRight, tf, 11, Brushes.SeaGreen, 1.0), new Point(centerX + 8, y1 + 4));
                    }
                    y += stepY;
                }
            }

            string path = Path.GetFullPath($"flowchart_gost_{DateTime.Now:HHmmss}.png");
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path);
            enc.Save(fs);
            return path;
        }
    }
}




