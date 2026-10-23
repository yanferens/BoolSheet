using System.Numerics;
using BoolSheet.Core.Ast;
using BoolSheet.Core.Parsing;
using Xunit;

namespace BoolSheet.Tests;

public class FormulaParserTests
{
    private readonly FormulaParser _parser = new();

    private readonly FakeCells _cells = new(new Dictionary<string, BigInteger>
    {
        ["A1"] = 5,
        ["B1"] = 10,
    });

    [Theory]
    [InlineData("10 mod 3 = 1", true)]
    [InlineData("2 + 3 * 4 = 14", true)]
    [InlineData("(2 + 3) * 4 = 20", true)]
    [InlineData("10 - 3 - 2 = 5", true)]
    [InlineData("7 div 2 = 3", true)]
    [InlineData("(0 - 7) / 2 = 0 - 3", true)]
    [InlineData("(0 - 7) div 2 = 0 - 4", true)]
    [InlineData("(0 - 7) mod 2 = 1", true)]
    [InlineData("not 1 > 2", true)]
    [InlineData("NOT 0", true)]
    [InlineData("not 5", false)]
    [InlineData("(1 < 2) = (3 < 4)", true)]
    [InlineData("A1 * 2 <> B1", false)]
    [InlineData("a1 >= 5", true)]
    [InlineData("2 + 3", true)]
    [InlineData("2 - 2", false)]
    [InlineData("123456789012345678901234567890 + 1 > 123456789012345678901234567890", true)]
    public void Evaluate_ValidExpression_ReturnsExpectedLogicalValue(string expression, bool expected)
    {
        BigInteger value = _parser.Parse(expression).Evaluate(_cells);

        Assert.Equal(expected, Logic.IsTrue(value));
    }

    [Theory]
    [InlineData("1 < 2 < 3", 6)]   // comparisons cannot be chained
    [InlineData("A1 +", 4)]        // missing right operand
    [InlineData("(1 + 2", 6)]      // missing closing parenthesis
    [InlineData("2 & 3", 2)]       // invalid character
    [InlineData("", 0)]
    public void ValidateSyntax_InvalidExpression_ReportsErrorPosition(string expression, int position)
    {
        ValidationResult result = _parser.ValidateSyntax(expression);

        Assert.False(result.IsValid);
        Assert.Equal(position, result.ErrorPosition);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void Parse_InvalidExpression_ThrowsFormulaSyntaxException()
    {
        Assert.Throws<FormulaSyntaxException>(() => _parser.Parse("1 +"));
    }

    [Fact]
    public void Evaluate_DivisionByZero_ThrowsEvaluationException()
    {
        INode tree = _parser.Parse("1 div 0 = 0");

        Assert.Throws<EvaluationException>(() => tree.Evaluate(_cells));
    }

    [Fact]
    public void Evaluate_EmptyCellReference_ThrowsEvaluationException()
    {
        INode tree = _parser.Parse("C7 = 0");

        Assert.Throws<EvaluationException>(() => tree.Evaluate(_cells));
    }

    private sealed class FakeCells : ICellValueProvider
    {
        private readonly Dictionary<string, BigInteger> _values;

        public FakeCells(Dictionary<string, BigInteger> values)
        {
            _values = values;
        }

        public BigInteger GetCellValue(string address)
            => _values.TryGetValue(address, out BigInteger value)
                ? value
                : throw new EvaluationException($"Клітинка {address} порожня");
    }
}
