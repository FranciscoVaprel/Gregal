// Gregal.csproj marca su ensamblado como solo-Windows (ver Program.cs). Sin lo
// mismo aquí, cada tipo de Gregal usado en un test (HistoricoDTO, TagEvent,
// PartidaAccumulator...) generaba un aviso CA1416 de falso positivo, aunque no
// son APIs específicas de Windows.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]
