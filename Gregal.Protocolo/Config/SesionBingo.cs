namespace Gregal.Config;

// Día de sesión (FECHA_SESION) de un momento dado. Las sesiones de las salas
// no acaban a medianoche sino a las 07:00 (decidido con el usuario,
// 2026-11-16): una partida jugada a las 00:30 del día 15 es de la sesión del
// 14. Lo usan Gregal (PartidaAccumulator, al cerrar cada partida) y
// BingoHistoricoEditor (el día que muestra al abrir, e importar un log).
// Los datos ya guardados con la fecha de calendario no se tocan.
public static class SesionBingo {
    public static readonly TimeSpan HoraCambioPorDefecto = new(7, 0, 0);

    /// <summary>
    /// Fecha de la sesión a la que pertenece <paramref name="momento"/>: antes
    /// de la hora de cambio, la del día anterior. Una hora de cambio fuera de
    /// 00:00-23:59:59 se toma como la de por defecto.
    /// </summary>
    public static DateTime FechaDe(DateTime momento, TimeSpan? horaCambio = null) {
        TimeSpan corte = horaCambio ?? HoraCambioPorDefecto;
        if (corte < TimeSpan.Zero || corte >= TimeSpan.FromDays(1)) corte = HoraCambioPorDefecto;
        return momento.TimeOfDay < corte ? momento.Date.AddDays(-1) : momento.Date;
    }
}
