using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UCA_Mobile.Engines
{
    public static class MobileCodeExportEngine
    {
        public static Dictionary<string, string> GenerateAll(string formulaOrFlow)
        {
            string clean = string.IsNullOrWhiteSpace(formulaOrFlow) ? "sin(x) + x^2" : formulaOrFlow.Trim();
            string pyExpr = clean.Replace("^", "**");
            string cppExpr = Regex.Replace(clean, @"([a-zA-Z0-9\)]+)\^([a-zA-Z0-9\(\)]+)", "pow($1, $2)");

            string py = $"# UCA Mobile Code Studio (Python 3)\nfrom math import sin, cos, tan, sqrt, exp, log, pi\n\ndef f(x, y=1.0, a=1.0):\n    return {pyExpr}\n\nfor x in range(-5, 6):\n    print(f'x={{x}} -> y={{f(x):.4f}}')";
            string cpp = $"// UCA Mobile Code Studio (C++17)\n#include <iostream>\n#include <cmath>\nusing namespace std;\n\ndouble f(double x, double y = 1.0, double a = 1.0) {{ return {cppExpr}; }}\n\nint main() {{\n    for (double x = -5; x <= 5; x += 1.0)\n        cout << \"x=\" << x << \" -> y=\" << f(x) << endl;\n    return 0;\n}}";
            string ino = $"// UCA Mobile Code Studio (Arduino .ino)\n#include <math.h>\nunsigned long lastMs = 0;\ndouble f(double x, double y = 1.0, double a = 1.0) {{ return {cppExpr}; }}\nvoid setup() {{ Serial.begin(115200); }}\nvoid loop() {{\n    if (millis() - lastMs >= 50) {{ lastMs = millis(); Serial.println(f(analogRead(A0)/204.6), 4); }}\n}}";

            return new Dictionary<string, string> { ["Python 3"] = py, ["C++ 17"] = cpp, ["Arduino (.ino)"] = ino };
        }
    }
}


