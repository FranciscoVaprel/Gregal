using Gregal.Parsing;

public class TagEventParserTests {
    [Fact]
    public void Parse_ConCodigoYValor_SeparaCorrectamente() {
        var evt = TagEventParser.Parse("*E19");

        Assert.NotNull(evt);
        Assert.Equal('E', evt!.Value.Code);
        Assert.Equal("19", evt.Value.Value);
    }

    [Fact]
    public void Parse_SinValor_DevuelveValorVacio() {
        var evt = TagEventParser.Parse("*D");

        Assert.NotNull(evt);
        Assert.Equal('D', evt!.Value.Code);
        Assert.Equal(string.Empty, evt.Value.Value);
    }

    [Fact]
    public void Parse_SinAsteriscoInicial_FuncionaIgual() {
        var evt = TagEventParser.Parse("F");

        Assert.NotNull(evt);
        Assert.Equal('F', evt!.Value.Code);
        Assert.Equal(string.Empty, evt.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("   ")]
    public void Parse_SinContenidoUtil_DevuelveNull(string rawFragment) {
        Assert.Null(TagEventParser.Parse(rawFragment));
    }
}
