using System.Text.Json;
using Gregal.Diagnostics;
using Gregal.Models;

public class ReimportacionPendientesTests {
    private static string Linea(int partida, int sala = 29) =>
        JsonSerializer.Serialize(new HistoricoDTO { CodSala = sala, CodConc = 999, FechaSesion = "2026-02-03", PartidaDia = partida, Precio = 1 });

    [Fact]
    public async Task Procesar_GuardaCadaPartidaYNoDejaPendientes() {
        var guardadas = new List<int>();
        var lineas = new[] { Linea(1), "", Linea(2), "   " };

        var r = await ReimportacionPendientes.ProcesarAsync(lineas, dto => { guardadas.Add(dto.PartidaDia); return Task.FromResult(true); }, _ => { });

        Assert.Equal(new[] { 1, 2 }, guardadas);
        Assert.Equal(2, r.Guardadas);
        Assert.Equal(0, r.Fallidas);
        Assert.Equal(0, r.Ilegibles);
        Assert.Empty(r.LineasPendientes);
    }

    [Fact]
    public async Task Procesar_LasQueFallanSiguenPendientesTalCual() {
        var lineas = new[] { Linea(1), Linea(2), Linea(3) };

        var r = await ReimportacionPendientes.ProcesarAsync(lineas, dto => Task.FromResult(dto.PartidaDia != 2), _ => { });

        Assert.Equal(2, r.Guardadas);
        Assert.Equal(1, r.Fallidas);
        Assert.Equal(new[] { Linea(2) }, r.LineasPendientes);
    }

    [Fact]
    public async Task Procesar_UnaExcepcionAlGuardarCuentaComoFallo() {
        var r = await ReimportacionPendientes.ProcesarAsync(new[] { Linea(7) }, _ => throw new InvalidOperationException("sin conexión"), _ => { });

        Assert.Equal(0, r.Guardadas);
        Assert.Equal(1, r.Fallidas);
        Assert.Single(r.LineasPendientes);
    }

    [Fact]
    public async Task Procesar_LineasQueNoSonJsonSeConservanYSeAvisan() {
        var avisos = new List<string>();
        var lineas = new[] { "esto no es json", "null", Linea(4) };

        var r = await ReimportacionPendientes.ProcesarAsync(lineas, _ => Task.FromResult(true), avisos.Add);

        Assert.Equal(1, r.Guardadas);
        Assert.Equal(2, r.Ilegibles);
        Assert.Equal(new[] { "esto no es json", "null" }, r.LineasPendientes);
        Assert.Contains(avisos, a => a.Contains("no es una partida"));
        Assert.Contains(avisos, a => a.Contains("Sala 29/999, partida 4 del 03/02/2026") && a.Contains("guardada"));
    }

    [Fact]
    public async Task Procesar_LeeElMismoJsonQueEscribeElReceptor() {
        // El receptor serializa el DTO con las opciones por defecto; aquí se
        // comprueba que se recupera entero, premios incluidos.
        var original = new HistoricoDTO { CodSala = 5, CodConc = 999, FechaSesion = "2026-11-12", PartidaDia = 3, Bolas = "1,2,3", Venta = 123.45m };
        original.Premios[0] = 6.15m;
        HistoricoDTO? leido = null;

        await ReimportacionPendientes.ProcesarAsync(new[] { JsonSerializer.Serialize(original) }, dto => { leido = dto; return Task.FromResult(true); }, _ => { });

        Assert.NotNull(leido);
        Assert.Equal(5, leido!.CodSala);
        Assert.Equal("1,2,3", leido.Bolas);
        Assert.Equal(123.45m, leido.Venta);
        Assert.Equal(6.15m, leido.Premios[0]);
        Assert.Equal(99, leido.Premios.Length);
    }
}
