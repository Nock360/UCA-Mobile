using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using AngouriMath;
using AngouriMath.Extensions;

namespace UCA_Mobile.Engines
{
    public static class SimulationEngine
    {
        public static bool TryRunSimulation(string input, out List<string> steps, out string result, out List<Point> simPoints)
        {
            steps = new List<string>();
            result = "";
            simPoints = new List<Point>();
            string clean = input.Replace(" ", "");

            // 1. Многопоточный метод Монте-Карло до 50 000 000 испытаний
            var mcMatch = Regex.Match(clean, @"^montecarlo\((\d+)\)$", RegexOptions.IgnoreCase);
            if (mcMatch.Success)
            {
                long iterations = Math.Clamp(long.Parse(mcMatch.Groups.Cast<Group>().ElementAt(1).Value), 100, 50000000);
                long inside = 0;
                int chunks = Environment.ProcessorCount;
                long perChunk = iterations / chunks;

                System.Threading.Tasks.Parallel.For(0, chunks, c =>
                {
                    var rnd = new Random(Guid.NewGuid().GetHashCode());
                    long localInside = 0;
                    for (long i = 0; i < perChunk; i++)
                    {
                        double x = rnd.NextDouble(), y = rnd.NextDouble();
                        if (x * x + y * y <= 1.0) localInside++;
                    }
                    System.Threading.Interlocked.Add(ref inside, localInside);
                });

                long totalDone = perChunk * chunks;
                double piEst = 4.0 * inside / totalDone;
                var rndPlot = new Random();
                int pIn = 0;
                for (int k = 1; k <= 100; k++)
                {
                    for (int s = 0; s < 50; s++) { double x = rndPlot.NextDouble(), y = rndPlot.NextDouble(); if (x * x + y * y <= 1.0) pIn++; }
                    simPoints.Add(new Point(k * (totalDone / 100.0), 4.0 * pIn / (k * 50)));
                }

                result = $"π ≈ {piEst:F7} (ошибка {Math.Abs(Math.PI - piEst):E2})";
                steps.Add($"Параллельный Монте-Карло ({chunks} ядер CPU): {totalDone:N0} испытаний");
                steps.Add($"Оценка числа π = {piEst:F7} | Погрешность = {Math.Abs(Math.PI - piEst):F7}");
                return true;
            }

            // 2. Цикл табуляции: loop(f(x), start, end, step)
            var loopMatch = Regex.Match(clean, @"^loop\((.+),([+-]?\d+(?:\.\d+)?),([+-]?\d+(?:\.\d+)?),([+-]?\d+(?:\.\d+)?)\)$", RegexOptions.IgnoreCase);
            if (loopMatch.Success)
            {
                var g = loopMatch.Groups.Cast<Group>().ToList();
                Entity expr = g.ElementAt(1).Value;
                double start = double.Parse(g.ElementAt(2).Value, CultureInfo.InvariantCulture);
                double end = double.Parse(g.ElementAt(3).Value, CultureInfo.InvariantCulture);
                double step = double.Parse(g.ElementAt(4).Value, CultureInfo.InvariantCulture);
                var fn = expr.Compile("x");
                steps.Add($"Табуляция f(x) = {expr} на [{start}; {end}]:");
                for (double x = start; x <= end + 1e-9; x += step)
                {
                    double y = (double)fn.Call(x).Real;
                    simPoints.Add(new Point(x, y));
                    if (steps.Count <= 25) steps.Add($"x = {x:G4}  →  f(x) = {y:G6}");
                }
                result = $"Цикл выполнен ({simPoints.Count} точек)";
                return true;
            }

            // 3. Сумма ряда: sum(f(n), start, end)
            var sumMatch = Regex.Match(clean, @"^sum\((.+),(\d+),(\d+)\)$", RegexOptions.IgnoreCase);
            if (sumMatch.Success)
            {
                var g = sumMatch.Groups.Cast<Group>().ToList();
                Entity expr = g.ElementAt(1).Value;
                int nStart = int.Parse(g.ElementAt(2).Value), nEnd = int.Parse(g.ElementAt(3).Value);
                var fn = expr.Compile("n");
                double total = 0;
                int plotStep = Math.Max(1, (nEnd - nStart) / 80);
                for (int n = nStart; n <= nEnd; n++)
                {
                    total += (double)fn.Call(n).Real;
                    if ((n - nStart) % plotStep == 0) simPoints.Add(new Point(n, total));
                }
                result = $"Σ = {total:G8}";
                steps.Add($"Сумма ряда Σ ({expr}) от {nStart} до {nEnd}: S = {total:G10}");
                return true;
            }

            // 4. Численный поиск корня: root_num(cos(x)-x, 0, 1)
            var rootMatch = Regex.Match(clean, @"^root_num\((.+),([+-]?\d+(?:\.\d+)?),([+-]?\d+(?:\.\d+)?)\)$", RegexOptions.IgnoreCase);
            if (rootMatch.Success)
            {
                var g = rootMatch.Groups.Cast<Group>().ToList();
                Entity expr = g.ElementAt(1).Value;
                double a = double.Parse(g.ElementAt(2).Value, CultureInfo.InvariantCulture);
                double b = double.Parse(g.ElementAt(3).Value, CultureInfo.InvariantCulture);
                var fn = expr.Compile("x");

                for (int i = 0; i <= 80; i++) { double px = a + (b - a) * i / 80.0; simPoints.Add(new Point(px, (double)fn.Call(px).Real)); }

                double fa = (double)fn.Call(a).Real, fb = (double)fn.Call(b).Real;
                if (fa * fb > 0) throw new Exception("На концах отрезка функция имеет одинаковый знак.");

                double mid = a;
                for (int iter = 1; iter <= 30; iter++)
                {
                    mid = (a + b) / 2.0;
                    double fm = (double)fn.Call(mid).Real;
                    if (iter <= 8) steps.Add($"Итерация {iter}: x = {mid:F6}, f(x) = {fm:E2}");
                    if (Math.Abs(fm) < 1e-8) break;
                    if (fa * fm < 0) { b = mid; fb = fm; } else { a = mid; fa = fm; }
                }
                result = $"x ≈ {mid:F7}";
                steps.Add($"Корень уравнения {expr} = 0 найден с точностью 10⁻⁷: x = {mid:F7}");
                return true;
            }
            // 5. Дифференциальное уравнение y' = f(x, y) методом Рунге-Кутты 4: ode(x - y, 0, 1, 5)
            var odeMatch = Regex.Match(clean, @"^ode\((.+),([+-]?\d+(?:\.\d+)?),([+-]?\d+(?:\.\d+)?),([+-]?\d+(?:\.\d+)?)\)$", RegexOptions.IgnoreCase);
            if (odeMatch.Success)
            {
                var g = odeMatch.Groups.Cast<Group>().ToList();
                Entity expr = g.ElementAt(1).Value;
                double x0 = double.Parse(g.ElementAt(2).Value, CultureInfo.InvariantCulture);
                double y0 = double.Parse(g.ElementAt(3).Value, CultureInfo.InvariantCulture);
                double xEnd = double.Parse(g.ElementAt(4).Value, CultureInfo.InvariantCulture);
                var fn = expr.Compile("x", "y");

                int n = 100;
                double h = (xEnd - x0) / n;
                double x = x0, y = y0;
                simPoints.Add(new Point(x, y));

                for (int i = 0; i < n; i++)
                {
                    double k1 = (double)fn.Call(x, y).Real;
                    double k2 = (double)fn.Call(x + h / 2, y + h * k1 / 2).Real;
                    double k3 = (double)fn.Call(x + h / 2, y + h * k2 / 2).Real;
                    double k4 = (double)fn.Call(x + h, y + h * k3).Real;
                    y += (h / 6.0) * (k1 + 2 * k2 + 2 * k3 + k4);
                    x += h;
                    simPoints.Add(new Point(x, y));
                }

                result = $"y({xEnd}) ≈ {y:F6}";
                steps.Add($"Задача Коши: y' = {expr}, начальное условие y({x0}) = {y0}");
                steps.Add($"Метод Рунге-Кутты 4-го порядка (100 шагов, h = {h:G4}):");
                steps.Add($"Значение в конечной точке: y({xEnd}) = {y:F6}");
                return true;
            }

            return false;
        }
    }

    public static class ReportGenerator
    {
        public static (string HtmlPath, string LatexCode) ExportLabReport(string query, string answer, string stepsText, string? chartImagePath)
        {
            string cleanSteps = SmartInputAndFormat.BeautifyMath(stepsText);
            string latex = "\\documentclass[12pt,a4paper]{article}\n\\usepackage[utf8]{inputenc}\n\\usepackage[russian]{babel}\n\\usepackage{amsmath}\n\\begin{{document}}\n" +
                           $"\\section*{{Отчёт по расчёту (UCA Studio)}}\n\\textbf{{Исходное задание:}} \\texttt{{{query}}}\\\\\n" +
                           $"\\textbf{{Итоговый результат:}} \\texttt{{{answer}}}\n\\subsection*{{Ход решения}}\n\\begin{{verbatim}}\n{cleanSteps}\n\\end{{verbatim}}\n\\end{{document}}";

            string imgTag = (!string.IsNullOrEmpty(chartImagePath) && File.Exists(chartImagePath))
                ? $"<div class='card'><h3>Графическая визуализация</h3><img src='file:///{chartImagePath.Replace("\\", "/")}' style='max-width:100%; border:1px solid #ddd; border-radius:8px;'/></div>"
                : "";

            string html = $@"<!DOCTYPE html>
<html lang='ru'><head><meta charset='utf-8'><title>Отчёт UCA Studio</title>
<style>
  body {{ font-family: 'Segoe UI', sans-serif; max-width: 820px; margin: 40px auto; color: #18181b; line-height: 1.6; }}
  .header {{ border-bottom: 2px solid #18181b; padding-bottom: 12px; margin-bottom: 24px; }}
  .card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin-bottom: 18px; }}
  pre {{ font-family: Consolas, monospace; white-space: pre-wrap; font-size: 14px; }}
  @media print {{ .no-print {{ display: none; }} }}
</style></head><body>
  <div class='header'>
    <button class='no-print' onclick='window.print()' style='float:right; padding:8px 14px; background:#18181b; color:#fff; border:none; border-radius:6px; cursor:pointer;'>Сохранить в PDF / Печать</button>
    <h1 style='margin:0;'>Лабораторный отчёт расчёта</h1>
    <div style='color:#64748b; font-size:13px;'>Сформировано в UCA Studio • {DateTime.Now:dd.MM.yyyy HH:mm}</div>
  </div>
  <div class='card'><strong>Входное выражение:</strong><pre>{System.Net.WebUtility.HtmlEncode(query)}</pre></div>
  <div class='card'><strong>Итоговый результат:</strong><pre style='font-size:16px; font-weight:bold;'>{System.Net.WebUtility.HtmlEncode(answer)}</pre></div>
  <div class='card'><h3>Пошаговый протокол вычислений</h3><pre>{System.Net.WebUtility.HtmlEncode(cleanSteps)}</pre></div>
  {imgTag}
</body></html>";

            string htmlPath = Path.GetFullPath($"lab_report_{DateTime.Now:HHmmss}.html");
            File.WriteAllText(htmlPath, html, Encoding.UTF8);
            return (htmlPath, latex);
        }
    }
}




