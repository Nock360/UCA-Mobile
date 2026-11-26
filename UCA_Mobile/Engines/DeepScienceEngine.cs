using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Avalonia;
using AngouriMath;
using AngouriMath.Extensions;

namespace UCA_Mobile.Engines
{
    public static class DeepScienceEngine
    {
        public static bool TryProcessDeepScience(string input, out List<string> steps, out string result, out List<Point> plotPoints, out string plotTitle, out string? surface3DFormula)
        {
            steps = new List<string>();
            result = "";
            plotPoints = new List<Point>();
            plotTitle = "";
            surface3DFormula = null;

            string clean = input.Trim();

            // 1. Спектральный анализ сигнала (БПФ / FFT): fft: sin(2*pi*5*t) + 0.6*sin(2*pi*15*t)
            if (clean.StartsWith("fft:", StringComparison.OrdinalIgnoreCase))
            {
                string exprStr = clean.Substring(4).Trim().Replace("t", "x");
                Entity expr = exprStr;
                var fn = expr.Compile("x");

                int n = 256; // степень двойки для БПФ
                double sampleRate = 100.0; // 100 Гц
                var signal = new Complex[n];
                for (int i = 0; i < n; i++)
                {
                    double t = i / sampleRate;
                    double val = 0;
                    try { val = (double)fn.Call(t).Real; } catch { }
                    signal.SetValue(new Complex(val, 0), i);
                }

                var spectrum = ComputeFFT(signal);
                double maxAmp = 0, domFreq = 0;

                for (int k = 1; k < n / 2; k++)
                {
                    double freq = k * sampleRate / n;
                    double amp = (2.0 / n) * spectrum[k].Magnitude;
                    plotPoints.Add(new Point(freq, amp));
                    if (amp > maxAmp) { maxAmp = amp; domFreq = freq; }
                }

                var peaks = plotPoints.Where(p => p.Y > maxAmp * 0.25).OrderByDescending(p => p.Y).Take(4).ToList();
                result = $"Доминирующая частота: {domFreq:F2} Гц (Амплитуда = {maxAmp:F3})";
                plotTitle = "Амплитудный спектр Фурье (АЧХ, Гц)";
                steps.Add($"Спектральный анализ БПФ (N = {n} отсчётов, Fs = {sampleRate} Гц):");
                steps.Add($"Сигнал: s(t) = {clean.Substring(4).Trim()}");
                foreach (var pk in peaks)
                    steps.Add($"Гармоника: f = {pk.X:F2} Гц  →  амплитуда A = {pk.Y:F4}");
                return true;
            }
            // 2. Уравнения в частных производных: pde: heat (уравнение теплопроводности) или pde: wave (волновое уравнение)
            if (clean.StartsWith("pde:", StringComparison.OrdinalIgnoreCase))
            {
                string mode = clean.Substring(4).Trim().ToLower();
                int nx = 40, nt = 80;
                double[] u = new double[nx];
                double[] uPrev = new double[nx];

                if (mode.Contains("wave") || mode.Contains("волн"))
                {
                    for (int i = 0; i < nx; i++) { double x = (double)i / (nx - 1); u[i] = Math.Sin(Math.PI * x) + 0.5 * Math.Sin(3 * Math.PI * x); uPrev[i] = u[i]; }
                    double c = 0.4; // число Куранта
                    for (int t = 0; t < nt; t++)
                    {
                        double[] uNext = new double[nx];
                        for (int i = 1; i < nx - 1; i++)
                            uNext[i] = 2 * u[i] - uPrev[i] + c * c * (u[i + 1] - 2 * u[i] + u[i - 1]);
                        uPrev = u; u = uNext;
                    }
                    for (int i = 0; i < nx; i++) plotPoints.Add(new Point((double)i / (nx - 1), u[i]));
                    result = $"Волновое ур-е ∂²u/∂t² = a²∂²u/∂x² решено (t = {nt} шагов)";
                    plotTitle = "Профиль струны u(x, T)";
                    surface3DFormula = "sin(pi * (x + 5) / 10) * cos(2 * (y + 5))";
                    steps.Add("Уравнение в частных производных (гиперболический тип): ∂²u/∂t² = a² · ∂²u/∂x²");
                    steps.Add($"Сетка конечных разностей: {nx} узлов по пространству × {nt} шагов по времени");
                    steps.Add($"Максимальное отклонение струны в момент T: {u.Max(v => Math.Abs(v)):F4}");
                }
                else
                {
                    for (int i = 0; i < nx; i++) { double x = (double)i / (nx - 1); u[i] = 100.0 * Math.Sin(Math.PI * x); }
                    double r = 0.25;
                    for (int t = 0; t < nt; t++)
                    {
                        double[] uNext = new double[nx];
                        for (int i = 1; i < nx - 1; i++)
                            uNext[i] = u[i] + r * (u[i + 1] - 2 * u[i] + u[i - 1]);
                        u = uNext;
                    }
                    for (int i = 0; i < nx; i++) plotPoints.Add(new Point((double)i / (nx - 1), u[i]));
                    result = $"Ур-е теплопроводности ∂u/∂t = α∂²u/∂x² (T_max = {u.Max():F2}°C)";
                    plotTitle = "Распределение температуры u(x, T)";
                    surface3DFormula = "10 * exp(-0.3 * (y + 5)) * sin(pi * (x + 5) / 10)";
                    steps.Add("Уравнение теплопроводности Фурье (параболический тип): ∂u/∂t = α · ∂²u/∂x²");
                    steps.Add($"Начальный нагрев T₀ = 100°C, граничные условия u(0,t) = u(L,t) = 0");
                    steps.Add($"Максимальная температура в центре стержня после {nt} шагов: {u.Max():F3}°C");
                }
                return true;
            }

            // 3. Тензорное (кронекерово) произведение: tensor: [[1, 2],] x [[0, 5],]
            if (clean.StartsWith("tensor:", StringComparison.OrdinalIgnoreCase))
            {
                string body = clean.Substring(7).Trim();
                var parts = Regex.Split(body, @"\s*[x×*]\s*", RegexOptions.IgnoreCase);
                if (parts.Length == 2)
                {
                    var a = ParseMat(parts.First());
                    var b = ParseMat(parts.Last());
                    int rA = a.Count, cA = a.First().Count, rB = b.Count, cB = b.First().Count;
                    var kron = new List<string>();
                    for (int i = 0; i < rA; i++)
                        for (int k = 0; k < rB; k++)
                        {
                            var rowVals = new List<string>();
                            for (int j = 0; j < cA; j++)
                                for (int l = 0; l < cB; l++)
                                    rowVals.Add($"{a.ElementAt(i).ElementAt(j) * b.ElementAt(k).ElementAt(l):G4}");
                            kron.Add("[ " + string.Join(",  ", rowVals) + " ]");
                        }
                    result = $"Тензор ранга ({rA * rB} × {cA * cB}) вычислен";
                    steps.Add($"Тензорное (кронекерово) произведение A ⊗ B размерности {rA * rB}×{cA * cB}:");
                    steps.AddRange(kron);
                    return true;
                }
            }

            return false;
        }

        private static List<List<double>> ParseMat(string s)
        {
            var res = new List<List<double>>();
            foreach (Match m in Regex.Matches(s.Trim().TrimStart('[').TrimEnd(']'), @"\[([^\[\]]+)\]"))
                res.Add(m.Groups.Cast<Group>().ElementAt(1).Value.Split(',').Select(v => double.Parse(v.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToList());
            return res;
        }

        private static Complex[] ComputeFFT(Complex[] buffer)
        {
            int n = buffer.Length;
            if (n <= 1) return buffer;
            var even = ComputeFFT(buffer.Where((_, i) => i % 2 == 0).ToArray());
            var odd = ComputeFFT(buffer.Where((_, i) => i % 2 != 0).ToArray());
            var result = new Complex[n];
            for (int k = 0; k < n / 2; k++)
            {
                Complex t = Complex.FromPolarCoordinates(1.0, -2 * Math.PI * k / n) * odd[k];
                result[k] = even[k] + t;
                result[k + n / 2] = even[k] - t;
            }
            return result;
        }
    }
}



