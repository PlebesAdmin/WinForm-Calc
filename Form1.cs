// Form1.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Calc_App
{
    public class Form1 : Form
    {
        private readonly TextBox txtSize = new TextBox();
        private readonly TextBox txtValues = new TextBox();
        private readonly TextBox txtResult = new TextBox();
        private readonly CheckBox chk32nds = new CheckBox();
        private readonly CheckBox chkX10Off = new CheckBox();
        private readonly CheckBox chkOut32nds = new CheckBox();

        public Form1()
        {
            Text = "FinMarketCalc";
            StartPosition = FormStartPosition.CenterScreen;

            // Taskbar-like compact strip
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 1120;
            Height = 98;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10, 12, 10, 10)
            };

            var lblSize = new Label { Text = "SIZE", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
            txtSize.Width = 140;
            txtSize.Margin = new Padding(0, 2, 18, 0);

            var lblValues = new Label { Text = "Values", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
            txtValues.Width = 360;
            txtValues.Margin = new Padding(0, 2, 18, 0);

            var lblResult = new Label { Text = "Result", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
            txtResult.Width = 240;
            txtResult.ReadOnly = true;
            txtResult.TabStop = false;
            txtResult.Margin = new Padding(0, 2, 18, 0);

            chk32nds.Text = "32nd";
            chk32nds.AutoSize = true;
            chk32nds.Margin = new Padding(0, 4, 14, 0);

            chkOut32nds.Text = "Out 32nd";
            chkOut32nds.AutoSize = true;
            chkOut32nds.Margin = new Padding(0, 4, 14, 0);

            chkX10Off.Text = "x10 OFF";
            chkX10Off.AutoSize = true;
            chkX10Off.Margin = new Padding(0, 4, 0, 0);

            // Tab order
            txtSize.TabIndex = 0;
            txtValues.TabIndex = 1;
            chk32nds.TabIndex = 2;
            chkOut32nds.TabIndex = 3;
            chkX10Off.TabIndex = 4;

#if NET6_0_OR_GREATER
            txtSize.PlaceholderText = "e.g. 200";
            txtValues.PlaceholderText = "e.g. 99 - 1   or   99-16 + 99-12   or   99-16+";
#endif

            flow.Controls.Add(lblSize);
            flow.Controls.Add(txtSize);
            flow.Controls.Add(lblValues);
            flow.Controls.Add(txtValues);
            flow.Controls.Add(lblResult);
            flow.Controls.Add(txtResult);
            flow.Controls.Add(chk32nds);
            flow.Controls.Add(chkOut32nds);
            flow.Controls.Add(chkX10Off);

            Controls.Add(flow);

            // Enter calculates anywhere
            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    CalculateAndDisplay();
                }
            };

            chk32nds.CheckedChanged += (_, __) => CalculateAndDisplay();
            chkX10Off.CheckedChanged += (_, __) => CalculateAndDisplay();
            chkOut32nds.CheckedChanged += (_, __) => CalculateAndDisplay();
            txtSize.Leave += (_, __) => CalculateAndDisplay();
            txtValues.Leave += (_, __) => CalculateAndDisplay();
        }

        private void CalculateAndDisplay()
        {
            try
            {
                // SIZE parsing (blank/null => 0)
                decimal sizeNominal = 0m;
                if (!string.IsNullOrWhiteSpace(txtSize.Text))
                {
                    if (!decimal.TryParse(txtSize.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out sizeNominal))
                        throw new FormatException("SIZE is not a valid number.");
                }

                // Default is SIZE × 10 unless SIZE is empty/0 OR x10 OFF is checked
                decimal size = sizeNominal;
                if (sizeNominal != 0m && !chkX10Off.Checked)
                    size = sizeNominal * 10m;

                // Values expression
                string exprRaw = txtValues.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(exprRaw))
                {
                    txtResult.Text = string.Empty;
                    return;
                }

                decimal valuesResult = EvaluateValuesExpression(exprRaw, chk32nds.Checked);

                // Final result: if SIZE is empty/0 -> Field2 calc only; else SIZE × Field2
                decimal final = (sizeNominal == 0m) ? valuesResult : (size * valuesResult);

                // Optional output formatting back to 32nds
                if (chkOut32nds.Checked)
                {
                    txtResult.Text = DecimalTo32ndsPretty(final);
                }
                else
                {
                    txtResult.Text = final.ToString("0.########", CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                txtResult.Text = $"Error: {ex.Message}";
            }
        }

        private decimal EvaluateValuesExpression(string input, bool use32nds)
        {
            // Accept "x" as multiply and ignore a trailing "="
            string expr = (input ?? string.Empty)
                .Trim()
                .TrimEnd('=')
                .Replace('X', '*')
                .Replace('x', '*');

            if (use32nds)
            {
                expr = NormalizeTreasuryInput(expr);
                expr = Replace32ndsWithDecimal(expr);
            }

            return EvaluateArithmetic(expr);
        }

        /// <summary>
        /// Auto-normalise common desk typing patterns:
        /// - Fix dash variants and spaces around the hyphen only for 2-3 digit RHS (keeps "99 - 1" intact)
        /// - Normalise fractions "1 / 8" -> "1/8"
        /// - Convert "99-16 +" (suffix plus-tick) -> "99-16+" but keep arithmetic "99-16 + 99-12"
        /// </summary>
        private static string NormalizeTreasuryInput(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return expr;

            expr = expr
                .Replace('–', '-')
                .Replace('—', '-')
                .Replace('−', '-')
                .Trim();

            // normalise hyphen spacing for 32nds/256ths tokens
            expr = Regex.Replace(expr, @"\b(\d+)\s*-\s*(\d{2,3})\b", "$1-$2");

            // normalise fractions like 1 / 8, 3/ 8, etc.
            expr = Regex.Replace(expr, @"\b([13]|[1357])\s*/\s*(4|8)\b", "$1/$2");

            // normalise plus-tick suffix: 99-16 +  -> 99-16+
            // Only treat as suffix when followed by end, ')', or an operator.
            expr = Regex.Replace(expr, @"\b(\d+)-(\d{2})\s*\+\s*(?=($|[)\-*/+]))", "$1-$2+");

            // collapse repeated spaces
            expr = Regex.Replace(expr, @"\s{2,}", " ");

            return expr;
        }

        /// <summary>
        /// Replace treasury 32nds/256ths tokens with decimal literals.
        /// Supports:
        /// - 256ths: 99-001 => 99 + 1/256
        /// - 32nds:  99-16
        /// - plus:   99-16+  ("+" = 1/2 of a 32nd)
        /// - frac:   91-02 1/8 etc. (fraction of a 32nd)
        /// </summary>
        private static string Replace32ndsWithDecimal(string expr)
        {
            // 256ths style: 99-001
            var rx256 = new Regex(@"\b(\d+)-(\d{3})(?=([+\-*/)]|$))");
            expr = rx256.Replace(expr, m =>
            {
                int handle = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int th256 = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (th256 < 0 || th256 > 255)
                    throw new FormatException($"Invalid 256ths value '{m.Value}' (must be 000-255).");

                decimal dec = handle + (th256 / 256m);
                return dec.ToString("0.################", CultureInfo.InvariantCulture);
            });

            // 32nds style with optional suffix (+ or fraction)
            var rx32 = new Regex(@"\b(\d+)-(\d{2})(?:\s*(\+|[1357]/8|[13]/4))?(?=([+\-*/)]|$))");
            expr = rx32.Replace(expr, m =>
            {
                int handle = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int th32 = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                string suffix = m.Groups[3].Success ? m.Groups[3].Value : string.Empty;

                if (th32 < 0 || th32 > 31)
                    throw new FormatException($"Invalid 32nds value '{m.Value}' (must be 00-31).");

                decimal dec = handle + (th32 / 32m);

                if (!string.IsNullOrEmpty(suffix))
                {
                    decimal fracOf32nd;
                    if (suffix == "+")
                    {
                        // plus is 1/2 of a 32nd
                        fracOf32nd = 0.5m;
                    }
                    else
                    {
                        var parts = suffix.Split('/');
                        int num = int.Parse(parts[0], CultureInfo.InvariantCulture);
                        int den = int.Parse(parts[1], CultureInfo.InvariantCulture);
                        fracOf32nd = num / (decimal)den;
                    }

                    dec += (fracOf32nd / 32m);
                }

                return dec.ToString("0.################", CultureInfo.InvariantCulture);
            });

            return expr;
        }

        // -------- Output formatting --------

        /// <summary>
        /// Convert a decimal into a "handle-32nds" string with optional fractional suffix.
        /// Rounds to nearest 1/256 (i.e., 1/8 of a 32nd) and prints:
        /// - none for 0/8
        /// - "+" for 4/8
        /// - "1/8, 3/8, 5/8, 7/8" for odd eighths
        /// - "1/4" for 2/8, "3/4" for 6/8
        /// </summary>
        private static string DecimalTo32ndsPretty(decimal value)
        {
            // Handle negatives
            string sign = value < 0 ? "-" : "";
            decimal abs = Math.Abs(value);

            int handle = (int)Math.Floor(abs);
            decimal frac = abs - handle;

            int ticks256 = (int)Math.Round(frac * 256m, MidpointRounding.AwayFromZero);
            if (ticks256 == 256)
            {
                handle += 1;
                ticks256 = 0;
            }

            int th32 = ticks256 / 8;      // 0..31
            int eighths = ticks256 % 8;   // 0..7

            string basePart = $"{sign}{handle}-{th32:00}";
            if (eighths == 0) return basePart;

            // Map eighths-of-a-32nd to suffix
            // 2/8 => 1/4, 4/8 => '+', 6/8 => 3/4
            string suffix = eighths switch
            {
                4 => "+",
                2 => " 1/4",
                6 => " 3/4",
                _ => $" {eighths}/8"
            };

            // plus-tick printed without slash (common desk format)
            return eighths == 4 ? basePart + "+" : basePart + suffix;
        }

        // -------- Expression evaluator (shunting-yard + RPN) --------

        private static decimal EvaluateArithmetic(string expr)
        {
            var output = new List<string>();
            var ops = new Stack<char>();
            var tokens = Tokenise(expr);

            int Prec(char op) => (op == '+' || op == '-') ? 1 : 2;

            foreach (var t in tokens)
            {
                if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    output.Add(t);
                }
                else if (t.Length == 1 && "+-*/".Contains(t[0]))
                {
                    char op = t[0];
                    while (ops.Count > 0 && "+-*/".Contains(ops.Peek()) && Prec(ops.Peek()) >= Prec(op))
                        output.Add(ops.Pop().ToString());
                    ops.Push(op);
                }
                else if (t == "(")
                {
                    ops.Push('(');
                }
                else if (t == ")")
                {
                    while (ops.Count > 0 && ops.Peek() != '(')
                        output.Add(ops.Pop().ToString());
                    if (ops.Count == 0 || ops.Pop() != '(')
                        throw new FormatException("Mismatched parentheses.");
                }
                else
                {
                    throw new FormatException($"Invalid token '{t}'.");
                }
            }

            while (ops.Count > 0)
            {
                char op = ops.Pop();
                if (op == '(') throw new FormatException("Mismatched parentheses.");
                output.Add(op.ToString());
            }

            var stack = new Stack<decimal>();
            foreach (var t in output)
            {
                if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                {
                    stack.Push(n);
                }
                else
                {
                    if (stack.Count < 2) throw new FormatException("Invalid expression.");
                    decimal b = stack.Pop();
                    decimal a = stack.Pop();
                    stack.Push(t[0] switch
                    {
                        '+' => a + b,
                        '-' => a - b,
                        '*' => a * b,
                        '/' => b == 0m ? throw new DivideByZeroException("Division by zero.") : a / b,
                        _ => throw new FormatException($"Unknown operator '{t}'.")
                    });
                }
            }

            if (stack.Count != 1) throw new FormatException("Invalid expression.");
            return stack.Pop();
        }

        private static List<string> Tokenise(string expr)
        {
            var tokens = new List<string>();
            int i = 0;

            // remove spaces
            expr = (expr ?? string.Empty).Replace(" ", "");

            while (i < expr.Length)
            {
                char c = expr[i];

                // Number (supports unary minus)
                if (char.IsDigit(c) || c == '.' || (c == '-' && (i == 0 || "+-*/(".Contains(expr[i - 1]))))
                {
                    int start = i;
                    i++;
                    while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.'))
                        i++;
                    tokens.Add(expr.Substring(start, i - start));
                    continue;
                }

                if ("+-*/()".Contains(c))
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                throw new FormatException($"Unexpected character '{c}'.");
            }

            return tokens;
        }
    }
}
