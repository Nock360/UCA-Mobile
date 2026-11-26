using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;

namespace UCA_Mobile.Engines
{
    public static class PhysicsLabEngine
    {
        public static bool TryProcessPhysics(string input, out List<string> steps, out string result, out List<Point> trajectory)
        {
            steps = new List<string>();
            result = "";
            trajectory = new List<Point>();
            string clean = input.Trim();

            if (clean.StartsWith("ohm(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveOhm(clean, out steps, out result);
            if (clean.StartsWith("res(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveResistors(clean, out steps, out result);
            if (clean.StartsWith("cap(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveCapacitors(clean, out steps, out result);
            if (clean.StartsWith("div(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveDivider(clean, out steps, out result);
            if (clean.StartsWith("energy(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveEnergy(clean, out steps, out result);
            if (clean.StartsWith("lc(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveLC(clean, out steps, out result);
            if (clean.StartsWith("throw(", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(")"))
                return SolveProjectile(clean, out steps, out result, out trajectory);

            return false;
        }

        private static Dictionary<string, double> ParseParams(string call)
        {
            var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string inner = call.Substring(call.IndexOf('(') + 1).TrimEnd(')');
            foreach (var part in inner.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                    dict[kv.First().Trim()] = double.Parse(kv.Last().Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
            }
            return dict;
        }

        private static bool SolveOhm(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            var p = ParseParams(input);
            double u = p.ContainsKey("U") ? p["U"] : double.NaN;
            double i = p.ContainsKey("I") ? p["I"] : double.NaN;
            double r = p.ContainsKey("R") ? p["R"] : double.NaN;

            if (double.IsNaN(i) && !double.IsNaN(u) && !double.IsNaN(r)) i = u / r;
            else if (double.IsNaN(u) && !double.IsNaN(i) && !double.IsNaN(r)) u = i * r;
            else if (double.IsNaN(r) && !double.IsNaN(u) && !double.IsNaN(i)) r = u / i;
            else throw new Exception("Укажите любые 2 параметра: ohm(U=12, R=4)");

            result = $"U={u:G4}В, I={i:G4}А, R={r:G4}Ом";
            steps.Add($"Закон Ома: I = U / R");
            steps.Add($"U = {u:G5} В | I = {i:G5} А | R = {r:G5} Ом");
            steps.Add($"Мощность цепи: P = U·I = {u * i:G5} Вт");
            return true;
        }

        private static bool SolveResistors(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            var vals = input.Substring(4, input.Length - 5).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => double.Parse(x.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToList();
            double rSeries = vals.Sum();
            double rParallel = 1.0 / vals.Sum(r => 1.0 / r);
            result = $"R_посл={rSeries:G5} Ом | R_пар={rParallel:G5} Ом";
            steps.Add($"Номиналы: {string.Join(", ", vals)} Ом");
            steps.Add($"Последовательно: R = {rSeries:G6} Ом | Параллельно: R = {rParallel:G6} Ом");
            return true;
        }

        private static bool SolveCapacitors(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            var vals = input.Substring(4, input.Length - 5).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => double.Parse(x.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToList();
            double cParallel = vals.Sum();
            double cSeries = 1.0 / vals.Sum(c => 1.0 / c);
            result = $"C_пар={cParallel:G5} Ф | C_посл={cSeries:G5} Ф";
            steps.Add($"Ёмкости: {string.Join(", ", vals)} Ф");
            steps.Add($"Параллельно: C = {cParallel:G6} Ф | Последовательно: C = {cSeries:G6} Ф");
            return true;
        }
        private static bool SolveDivider(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            var p = ParseParams(input);
            double u = p.ContainsKey("U") ? p["U"] : 12.0;
            double r1 = p["R1"], r2 = p["R2"];
            double i = u / (r1 + r2);
            double uOut = u * (r2 / (r1 + r2));
            result = $"U_out = {uOut:G5} В (I = {i * 1000:G4} мА)";
            steps.Add($"Делитель напряжения: U_out = U · R2 / (R1 + R2)");
            steps.Add($"Выходное напряжение U_out = {uOut:G6} В");
            steps.Add($"Ток через делитель I = {i:G5} А | Мощность P = {u * i:G5} Вт");
            return true;
        }

        private static bool SolveEnergy(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            var p = ParseParams(input);
            double m = p.ContainsKey("m") ? p["m"] : 1.0;
            double v = p.ContainsKey("v") ? p["v"] : 0.0;
            double h = p.ContainsKey("h") ? p["h"] : 0.0;
            double ek = 0.5 * m * v * v;
            double ep = m * 9.80665 * h;
            result = $"E_полн = {ek + ep:G5} Дж";
            steps.Add($"Масса m = {m} кг, скорость v = {v} м/с, высота h = {h} м");
            steps.Add($"Кинетическая энергия E_k = m·v²/2 = {ek:G6} Дж");
            steps.Add($"Потенциальная энергия E_p = m·g·h = {ep:G6} Дж");
            steps.Add($"Полная механическая энергия E = {ek + ep:G6} Дж");
            return true;
        }

        private static bool SolveLC(string input, out List<string> steps, out string result)
        {
            steps = new List<string>();
            var p = ParseParams(input);
            double l = p["L"], c = p["C"];
            double period = 2 * Math.PI * Math.Sqrt(l * c);
            double freq = 1.0 / period;
            result = $"f₀ = {freq:G5} Гц (T = {period:G4} с)";
            steps.Add($"Формула Томсона: T = 2π·√(L·C) = {period:G6} с");
            steps.Add($"Резонансная частота f₀ = {freq:G6} Гц");
            steps.Add($"Циклическая частота ω = {1.0 / Math.Sqrt(l * c):G6} рад/с");
            return true;
        }

        private static bool SolveProjectile(string input, out List<string> steps, out string result, out List<Point> trajectory)
        {
            steps = new List<string>();
            trajectory = new List<Point>();
            var p = ParseParams(input);
            double v0 = p.ContainsKey("v0") ? p["v0"] : 20.0;
            double deg = p.ContainsKey("angle") ? p["angle"] : 45.0;
            double h0 = p.ContainsKey("h0") ? p["h0"] : 0.0;
            double g = 9.80665, rad = deg * Math.PI / 180.0;

            double vx = v0 * Math.Cos(rad), vy = v0 * Math.Sin(rad);
            double tFlight = (vy + Math.Sqrt(vy * vy + 2 * g * h0)) / g;
            double maxH = h0 + (vy * vy) / (2 * g);
            double range = vx * tFlight;
            double vEnd = Math.Sqrt(vx * vx + Math.Pow(vy - g * tFlight, 2));

            for (int k = 0; k <= 120; k++)
            {
                double t = tFlight * k / 120.0;
                trajectory.Add(new Point(vx * t, h0 + vy * t - 0.5 * g * t * t));
            }

            result = $"L = {range:G5} м, H_max = {maxH:G5} м";
            steps.Add($"Бросок: v₀ = {v0} м/с, α = {deg}°, нач. высота h₀ = {h0} м");
            steps.Add($"Время полёта: t = {tFlight:G5} с | Скорость падения: {vEnd:G5} м/с");
            steps.Add($"Максимальная высота: H = {maxH:G5} м | Дальность полёта: L = {range:G5} м");
            return true;
        }
    }
}



