using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace UCA_Mobile.Engines
{
    public static class QuantumEngine
    {
        public static bool TryProcessQuantum(string input, out List<string> steps, out string result, out List<(string Label, double Prob)> probs)
        {
            steps = new List<string>();
            result = "";
            probs = new List<(string, double)>();
            string clean = input.Trim();

            if (!clean.StartsWith("q:", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("quantum:", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("квантовый:", StringComparison.OrdinalIgnoreCase) &&
                !clean.Equals("qbell", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("qghz", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("qrand", StringComparison.OrdinalIgnoreCase))
                return false;

            if (clean.Equals("qbell", StringComparison.OrdinalIgnoreCase))
                clean = "q: 2 | H(0) -> CNOT(0,1)";
            else if (clean.StartsWith("qghz", StringComparison.OrdinalIgnoreCase))
                clean = "q: 3 | H(0) -> CNOT(0,1) -> CNOT(1,2)";
            else if (clean.StartsWith("qrand", StringComparison.OrdinalIgnoreCase))
                clean = "q: 4 | H(0) -> H(1) -> H(2) -> H(3)";

            string body = clean.Substring(clean.IndexOf(':') + 1).Trim();
            int numQubits = 2;
            string circuitStr = body;

            if (body.Contains("|"))
            {
                var parts = body.Split('|');
                int.TryParse(parts.First().Trim(), out numQubits);
                numQubits = Math.Clamp(numQubits, 1, 4);
                circuitStr = parts.Last().Trim();
            }

            int dim = 1 << numQubits;
            var state = new Complex[dim];
            state.SetValue(Complex.One, 0);

            steps.Add($"Квантовый регистр: {numQubits} кубит(а), начальное состояние |{new string('0', numQubits)}⟩");

            var gates = circuitStr.Split(new[] { "->", ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawGate in gates)
            {
                string g = rawGate.Replace(" ", "").Trim();
                int pOpen = g.IndexOf('(');
                int pClose = g.IndexOf(')');
                if (pOpen <= 0 || pClose <= pOpen) continue;

                string name = g.Substring(0, pOpen).ToUpper();
                var args = g.Substring(pOpen + 1, pClose - pOpen - 1).Split(',');

                if (args.Length == 2)
                {
                    int q1 = int.Parse(args[0]);
                    int q2 = int.Parse(args[1]);
                    if (q1 >= numQubits || q2 >= numQubits || q1 == q2) throw new Exception($"Некорректные индексы кубитов в {g}");
                    if (name == "SWAP") ApplySwap(state, numQubits, q1, q2);
                    else ApplyCnot(state, numQubits, q1, q2);
                    steps.Add($"Гейт {name}(q{q1}, q{q2}) применен");
                }
                else if (args.Length == 1)
                {
                    int q = int.Parse(args[0]);
                    if (q >= numQubits) throw new Exception($"Кубит q{q} вне регистра ({numQubits} кубит)");
                    ApplySingleGate(state, numQubits, q, name);
                    steps.Add($"Гейт {name}(q{q}) применен");
                }
            }
            var stateTerms = new List<string>();
            for (int i = 0; i < dim; i++)
            {
                double p = state[i].Magnitude * state[i].Magnitude;
                string ket = "|" + Convert.ToString(i, 2).PadLeft(numQubits, '0') + "⟩";
                probs.Add((ket, p * 100.0));
                if (p > 1e-6)
                {
                    string amp = Math.Abs(state[i].Imaginary) < 1e-6
                        ? $"{state[i].Real:F3}"
                        : $"({state[i].Real:F2}{(state[i].Imaginary >= 0 ? "+" : "")}{state[i].Imaginary:F2}i)";
                    stateTerms.Add($"{amp}{ket}");
                    steps.Add($"Состояние {ket}: амплитуда = {amp}, вероятность = {p * 100.0:F2}%");
                }
            }

            double r = new Random().NextDouble(), acc = 0;
            string measured = probs.Last().Label;
            foreach (var item in probs)
            {
                acc += item.Prob / 100.0;
                if (r <= acc) { measured = item.Label; break; }
            }

            result = $"|ψ⟩ = {string.Join(" + ", stateTerms)}  |  Измерение: {measured}";
            steps.Add($"Вектор состояния: |ψ⟩ = {string.Join(" + ", stateTerms)}");
            steps.Add($"Результат одиночного измерения кубитов: {measured}");
            return true;
        }

        private static void ApplySingleGate(Complex[] state, int n, int target, string gate)
        {
            double invSqrt2 = 1.0 / Math.Sqrt(2.0);
            Complex m00, m01, m10, m11;
            switch (gate)
            {
                case "H": m00 = invSqrt2; m01 = invSqrt2; m10 = invSqrt2; m11 = -invSqrt2; break;
                case "X": m00 = 0; m01 = 1; m10 = 1; m11 = 0; break;
                case "Y": m00 = 0; m01 = -Complex.ImaginaryOne; m10 = Complex.ImaginaryOne; m11 = 0; break;
                case "Z": m00 = 1; m01 = 0; m10 = 0; m11 = -1; break;
                case "S": m00 = 1; m01 = 0; m10 = 0; m11 = Complex.ImaginaryOne; break;
                case "T": m00 = 1; m01 = 0; m10 = 0; m11 = Complex.FromPolarCoordinates(1.0, Math.PI / 4.0); break;
                default: throw new Exception($"Неизвестный гейт: {gate}");
            }

            int bit = 1 << (n - 1 - target);
            for (int i = 0; i < state.Length; i++)
            {
                if ((i & bit) == 0)
                {
                    int j = i | bit;
                    Complex a = state[i], b = state[j];
                    state[i] = m00 * a + m01 * b;
                    state[j] = m10 * a + m11 * b;
                }
            }
        }

        private static void ApplyCnot(Complex[] state, int n, int ctrl, int target)
        {
            int cBit = 1 << (n - 1 - ctrl);
            int tBit = 1 << (n - 1 - target);
            for (int i = 0; i < state.Length; i++)
            {
                if ((i & cBit) != 0 && (i & tBit) == 0)
                {
                    int j = i | tBit;
                    (state[i], state[j]) = (state[j], state[i]);
                }
            }
        }

        private static void ApplySwap(Complex[] state, int n, int q1, int q2)
        {
            ApplyCnot(state, n, q1, q2);
            ApplyCnot(state, n, q2, q1);
            ApplyCnot(state, n, q1, q2);
        }
    }
}




