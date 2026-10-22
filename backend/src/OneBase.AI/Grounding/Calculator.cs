using System.Globalization;
using System.Text.Json;
using OneBase.AI.Llm;

namespace OneBase.AI.Grounding;

/// <summary>
/// Calculation Validation: точный расчёт для AI-сотрудника (разница, сумма, доля, рост, выполнение плана) — считает код, а не модель.
/// Выражение: числа (дробная часть через точку или запятую), + − * / × ÷, скобки, «%» после числа (÷ 100). Результат — доказательство:
/// число из расчёта в ответе считается подтверждённым.
/// </summary>
public static class Calculator
{
    public const string ToolName = "calculate";

    private const int MaxLength = 300;

    public static readonly LlmToolDefinition Definition = new(
        ToolName,
        "Точный расчёт по числам из данных: разница, сумма, доля, рост в %, выполнение плана, среднее. Любое число ответа, которого нет в " +
        "результатах инструментов, считай только здесь — иначе оно не будет подтверждено.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                expression = new { type = "string", description = "Арифметика: числа, + - * / ( ), например (8200000-7100000)/7100000*100" },
                label = new { type = "string", description = "Что считается: «рост продаж к августу, %»" },
            },
            required = new[] { "expression" },
        }));

    /// <summary>Результат вызова инструмента: JSON для модели и само значение (null — ошибка в выражении).</summary>
    public static (string Content, decimal? Value) Run(JsonElement arguments)
    {
        var expression = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("expression", out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? ""
            : "";
        var label = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
        try
        {
            var value = Evaluate(expression);
            return (JsonSerializer.Serialize(new { label, expression, result = value }), value);
        }
        catch (FormatException ex)
        {
            return (JsonSerializer.Serialize(new { error = ex.Message, expression }), null);
        }
    }

    /// <summary>Значение выражения, округлённое до 6 знаков. FormatException — выражение не разобрать или деление на ноль.</summary>
    public static decimal Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > MaxLength)
        {
            throw new FormatException($"Нужно выражение до {MaxLength} символов.");
        }

        var parser = new Parser(expression.Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace('–', '-'));
        decimal value;
        try
        {
            value = parser.Expression();
        }
        catch (OverflowException)
        {
            throw new FormatException("Слишком большое число.");
        }
        catch (DivideByZeroException)
        {
            throw new FormatException("Деление на ноль.");
        }

        parser.End();
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private sealed class Parser(string text)
    {
        private int _i;

        public decimal Expression()
        {
            var value = Term();
            while (Peek() is '+' or '-')
            {
                var op = text[_i++];
                var right = Term();
                value = op == '+' ? value + right : value - right;
            }

            return value;
        }

        public void End()
        {
            if (Peek() is not '\0')
            {
                throw new FormatException($"Лишний символ «{text[_i]}» в позиции {_i + 1}.");
            }
        }

        private decimal Term()
        {
            var value = Factor();
            while (Peek() is '*' or '/')
            {
                var op = text[_i++];
                var right = Factor();
                value = op == '*' ? value * right : value / right;
            }

            return value;
        }

        private decimal Factor()
        {
            if (Peek() is '-' or '+')
            {
                var sign = text[_i++];
                var inner = Factor();
                return sign == '-' ? -inner : inner;
            }

            decimal value;
            if (Peek() == '(')
            {
                _i++;
                value = Expression();
                if (Peek() != ')')
                {
                    throw new FormatException("Не закрыта скобка.");
                }

                _i++;
            }
            else
            {
                value = Number();
            }

            if (Peek() == '%')
            {
                _i++;
                value /= 100m;
            }

            return value;
        }

        private decimal Number()
        {
            Peek();
            var start = _i;
            while (_i < text.Length && (char.IsAsciiDigit(text[_i]) || text[_i] is '.' or ','))
            {
                _i++;
            }

            var token = text[start.._i].Replace(',', '.');
            if (token.Length == 0 || !decimal.TryParse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException(start < text.Length ? $"Ожидалось число в позиции {start + 1}." : "Выражение оборвано.");
            }

            return value;
        }

        private char Peek()
        {
            while (_i < text.Length && char.IsWhiteSpace(text[_i]))
            {
                _i++;
            }

            return _i < text.Length ? text[_i] : '\0';
        }
    }
}
