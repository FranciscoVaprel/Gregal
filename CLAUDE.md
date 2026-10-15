# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Gregal is a .NET 10 Worker Service that receives bingo-hall session results ("historicos de partidas") and upserts them into a Firebird database. It understands two unrelated wire protocols, each its own `IHostedService`, enabled independently by config: a single-message JSON/CSV protocol over TCP and/or RS232 (the original design), and an event-stream RS232 protocol (`*CODIGO<valor>#`, one tiny event per ball/prize/etc., shared with the `Vaprel.PidFullHD` screen software) that accumulates a whole game before saving it — **this second one is the primary/default receiver today**. It doubles as a Windows Service (`GregalReceiverService`) in production. Solution: `Gregal.slnx` → `Gregal/Gregal.csproj` (the service) + `Gregal.Tests/Gregal.Tests.csproj` (xUnit tests).

## Commands

```
dotnet build                                   # build the whole solution
dotnet run --project Gregal                    # run the service as a console app
dotnet test                                    # run all tests
dotnet test --filter "FullyQualifiedName~JsonParserTests"   # run a single test/class
dotnet publish Gregal -c Release -p:PublishProfile=FolderProfile   # publish (win-x64, framework-dependent)
```

There is no separate lint step; `dotnet build` surfaces nullable/analyzer warnings (`Nullable` is enabled).

To install/run as a Windows service (see `Gregal/Docs/Dev_Anotaciones.txt`):
```
sc.exe create "GregalReceiverService" binPath= "C:\Deploy\Gregal\Gregal.exe" start= auto displayname= "Gregal - Receptor de Bingo"
```

## Architecture

### Three hosted services, enabled independently by config

`Program.cs` conditionally registers up to three independent `IHostedService` implementations, gated by two config flags (not by the vestigial `NetworkSettings.Mode`):

- `TagProtocolSettings.Enabled` → `Networking/TagSerialGregalReceiver.cs` — the event-stream protocol (`*CODIGO<valor>#`), its own RS232 port. **Enabled by default; today this is the only receiver actually running** (current `appsettings.json` has `NetworkSettings.Enabled=false`).
- `NetworkSettings.Enabled` → both `Networking/TcpGregalReceiver.cs` (TCP) and `Networking/SerialGregalReceiver.cs` (a second, independent serial port) — the original single-message JSON/CSV protocol. Disabled by default; kept for halls whose hardware still speaks it.

All three share the same `HistoricoRepository` (and the JSON/CSV pair also shares `GregalParserEngine`), but there is no shared base class or common interface between any of them (an earlier `Worker`/`IGregalReceiver` abstraction was removed as dead code) — each is a standalone `IHostedService`.

### JSON/CSV pipeline (`TcpGregalReceiver` / `SerialGregalReceiver`)

```
raw bytes → string accumulator → split on delimiter → GregalParserEngine.Parse()
   → ping short-circuit, else JsonGregalParser or CsvGregalParser (IParser) → ParseResult
   → HistoricoRepository.SaveHistoricoAsync(ParseResult.Data) → GregalProtocolFactory response
```

- One message = one immediate response, synchronously, on the same connection/port.
- Framing delimiter: `\0` for TCP (hardcoded) and `NetworkSettings.SerialDelimiter` for its own serial port (configurable, defaults to `\0`). Buffers are bounded by `NetworkSettings.TcpMaxBufferChars`/`MaxBufferChars`; exceeding it clears the buffer and replies `BUFFER_EXCEEDED`.
- `GregalParserEngine` (Parsing/GregalParserEngine.cs) sniffs the raw string, ignoring whitespace: `"operation":"ping"` / literal `ping` → pong; starts with `{` → `JsonGregalParser`; anything else → `CsvGregalParser`. **`CsvGregalParser` only fills a handful of `HistoricoDTO` fields by fixed column index** (msg_id, codSala, codConc, fecha, partidaDia, precio, venta, caja, bolas) — it's a partial legacy shim, not at parity with the JSON path.
- Both parsers call the shared `Parsing/HistoricoDtoValidator.ValidateRequiredFields` before returning a `ParseResult`, rejecting a `HistoricoDTO` that's missing `CodSala`/`CodConc`/`PartidaDia`/`FechaSesion` (the fields that make up the Firebird upsert key below) instead of letting an ambiguous `0`/empty key reach the database.
- Responses are always a `GregalResponse` JSON object serialized with a trailing `\0` (see `GregalProtocolFactory`), even on error — the client must never be left waiting.
- `TcpGregalReceiver.IsIpAuthorized` is fail-open by design: an empty/missing `NetworkSettings.AllowedIps` allows any IP ("empty = unrestricted"). Current config restricts to `127.0.0.1`; know this before clearing the list in a real deployment.

### Tag event-stream pipeline (`TagSerialGregalReceiver`) — primary protocol today

A different shape entirely: instead of one message per game, the hall hardware emits **dozens of tiny framed events per game** (`*E19#` = ball 19 drawn, `*C307#` = price, `*F#` = end of game, ...). There is no response — the original transport is one-way (UDP-derived, no ACK) and this receiver never writes back to the serial port.

```
raw bytes → _accumulator → split on TagProtocolSettings.FrameDelimiter ('#')
   → TagEventParser.Parse() → TagEvent → PartidaAccumulator.Feed()
   → (only on event 'F') HistoricoDTO → HistoricoDtoValidator
   → _persistChannel → PersistLoopAsync → HistoricoRepository.SaveHistoricoAsync
                                            (failure → appended to TagProtocolSettings.FallbackFilePath)
```

- `Parsing/PartidaAccumulator.cs` is stateful (one singleton instance, reused across games): it holds the in-progress game and only emits a `HistoricoDTO` on event `F`. Most of the mapping was confirmed on 2026-10-14 against a real 7-game capture from the Metronia hall and the tickets the machine itself prints per game (`Gregal.Tests/PartidaAccumulatorTests.cs`, `Feed_ConPartidaRealCompleta_CoincideConElTicketImpresoPorLaMaquina`) — notable confirmed decisions:
  - `C` (price) and the monetary `PremioCodeMap` codes (`L`, `p`, `A` — line, line-plus, prima) arrive in **cents**, not euros; `Finalizar()` divides by `10^NumDecimales` (from event `K`, defaulting to 2 if absent). `M` (ball-count cap for the prima prize, `PremioCodeMap["M"]` = `PREMIO7`) is explicitly excluded from that division — it's a ball number, not an amount. `Q`→`Caja` was left untouched — it never appeared in the real capture, so there's no evidence either way for its unit.
  - **Two different hardware manufacturers exist in the halls** (confirmed 2026-10-14, fourth round), with similar but non-identical protocols — Metronia (the 7-game capture above) and MCB (`VolcadoMCB.txt`). This explains what first looked like a contradiction: on Metronia, `PREMIO5`=line-plus (`p`), `PREMIO6`=prima (`A`), `PREMIO7`=prima ball cap (`M`), cross-checked against real ticket amounts; on MCB, the *same letters* carry no reliable value — they repeat constantly as junk/zero noise (`*M0#*A0#*A000#...`, several times between every single ball) rather than once per game with a real amount. `AplicarSiEsPremioMapeado` resolves this **per sala, by config**: `A`/`M`/`p` are in `CodigosSegunFabricante` — if a sala's `PremioCodeMap` doesn't include them (an MCB sala's `appsettings.json` shouldn't), they're silently ignored instead of logged as "unmapped" (which would flood the log given how often MCB repeats them). The *default* `PremioCodeMap` keeps them mapped (Metronia-style, since that's the one with positive ticket evidence) — whoever deploys to a new sala needs to check which manufacturer it has. `G`, `S`, `R` are **not** manufacturer-dependent, just confirmed-ignored on both (removed from the default map; the explicit `case` in `Aplicar` no-ops them before they'd ever be looked up there).
  - The MCB capture also exposed a real bug, now fixed: `*E<bola>#` values outside 1-90 (e.g. `*E101#`, confirmed noise — the `'e'` snapshot immediately before/after is unchanged) used to get appended to `Bolas` anyway; `Aplicar`'s `E` case now discards out-of-range values before touching `_bolasOrden`.
  - `D<tipo><cartón>#` (line) / `H<cartón>#` (bingo): confirmed against real tickets. The **first digit of `D`'s value is a line-type code** (maps to a row letter S/C/I on the printed ticket), not part of the card number — stripped before storing `CartLinea`. `H`'s value has no such prefix.
  - A **second, newer protocol variant** (confirmed by the hall operator, third round) merges line and bingo into one code, `Y<cartón>#`, with no type digit — which one it means depends on whether `*H#` (bare, no value) has already been seen this game: before it, `Y<cartón>` is a line; after it, a bingo. `*Y#` bare keeps its original meaning ("entering verification", no DTO effect). `PartidaAccumulator` supports **both** variants side by side (`RegistrarCantoLinea`/`RegistrarCantoBingo` are the shared bookkeeping `AplicarCantoLinea`/`AplicarEventoY` both call) since different hardware/firmware in the same hall could emit either.
  - Multiple winners in the same game are possible (confirmed: a real game had two valid bingo cards) — `CartLinea`/`CartBingo` store a comma-joined list, same convention as `Bolas`.
  - `D`/`u` and `H`/`h` really are "tentative call, reverted if the next matching event is wrong" (confirmed by the user) — none of the 7 real games happened to have an incorrect call, so `u`/`h` don't appear in that capture, but the mechanism itself is correct as designed. No equivalent revert exists yet for the `Y`-based variant.
  - Fields: `K`→`NumDecimales`, `P`/`U`→`Del1`/`Al1` (first/last card sold), `X`→`Serie1`/`Serie2` (comma-separated if the print run splices two series), `J`→`Letra1`/`Letra2` via `TagProtocolSettings.SerieLetraMap`. `J`'s value can carry trailing garbage after the 4-digit code (e.g. `386400001&BIPLUSE1.255` → use only `3864`) — confirmed third round. `Y` (bare), `º`, `f` (heartbeat), and now also `G`/`S`/`R`/`z`/`y` (lowercase) are recognized no-ops with no `HISTORICO` equivalent — confirmed, not placeholders — specifically so they don't spam the log at Information level the way a genuinely unmapped code would.
  - `Z<bola>#` removes a ball already registered (hardware misread correction) — `_bolasOrden` is a `List<int>` rather than an append-only string specifically to support this. The hall operator confirmed `Z` exists and corrected an earlier transcription error that had called it `X` (which is unambiguously the serie field, confirmed separately) — but `Z` never appeared in the 7 real games, so this path is implemented from the description alone, not validated against real traffic.
  - Still open: validating `EstadoPartidaDecoder`'s bit layout against a real `'e'` payload (the only attempt so far used a `.txt` capture that had been through a lossy Windows-1252 re-encoding, so it was inconclusive).
- Event `e` (lowercase) is a binary "game snapshot" used so a receiver that starts mid-game can catch up — decoded by `Parsing/EstadoPartidaDecoder.cs` (bit-packed ball mask + checksum, reverse-engineered from the external `CadenaEstado.cs` reference class, not part of Gregal). `PartidaAccumulator` merges in only the *newly learned* balls (balls already seen via real `E` events keep their real draw order; balls recovered only from the snapshot are appended in ascending order as an approximation) and marks the game as partially recovered in `Reclamacion` (capped at 64 chars — `HISTORICO.RECLAMACION` is `CHAR(64)`, fixed-length, confirmed via `RDB$FIELDS`).
- The serial port uses `Encoding.Latin1` (not the default ASCII/UTF-8) so bytes ≥ 0x80 in the binary `e` payload pass through unmodified — load-bearing, do not change it.
- Two-stage `Channel` pipeline (`_messageChannel` then `_persistChannel`) decouples serial reading from Firebird writes, so a burst of fast games never blocks ingestion. If `SaveHistoricoAsync` fails or returns `false`, the DTO is appended as JSON to `TagProtocolSettings.FallbackFilePath` instead of being lost (no automatic retry from that file back into Firebird — manual recovery only).
- `TagSerialGregalReceiver.Stop()` and `SerialGregalReceiver.Stop()` both guard against being invoked twice concurrently (`_stopLock`/`_detenido` fields) and swallow (log, don't throw) any exception from `SerialPort.Close()`/`Dispose()`. This exists because `Stop()` can be called from both `StopAsync` (host shutdown) and the listener loop's own `finally` at nearly the same time, and `System.IO.Ports.SerialPort` has a known internal `NullReferenceException` when closed/disposed concurrently — it used to crash the whole process on Ctrl+C. Preserve this pattern if you touch either class's shutdown path.
- `TagProtocolSettings.DefaultCodSala`/`DefaultCodConc` are placeholders (currently `1`/`1`, for testing) because the tag protocol carries no sala/concesión identity at all — a real deployment must set real values, and a single Gregal instance currently supports exactly one sala.

### Persistence

`Data/HistoricoRepository.cs` does a single Firebird `UPDATE OR INSERT ... MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)` against `HISTORICO`, inside a transaction, with the 99 `PREMIOn` columns built dynamically (`HistoricoDTO.Premios` is a `decimal?[99]`). All writes, from any of the three receivers, go through a single process-wide `static SemaphoreSlim(1,1)` so concurrent inserts never overlap — if you touch this method, preserve that serialization or justify removing it. Connection/transaction failures are caught and logged with sala/partida context, returning `false` rather than throwing.

### Configuration

Four strongly-typed options sections bound from `appsettings.json` via `IOptionsMonitor<T>` (Config/*.cs): `GeneralSettings`, `NetworkSettings` (TCP + its own serial port, gated by `NetworkSettings.Enabled`; `Mode` is vestigial/unused), `TagProtocolSettings` (its own separate serial port, gated by `TagProtocolSettings.Enabled`), `DatabaseSettings`. The two `Enabled` flags default differently (`NetworkSettings.Enabled=false`, `TagProtocolSettings.Enabled=true`) to match today's "tag protocol is primary" deployment.

The Firebird connection string in `appsettings.json` currently embeds a plaintext password (committed to git history, not just the working copy) — be careful not to further propagate that value when editing nearby code. `Gregal.csproj` has a `UserSecretsId` but it isn't actually used to keep the password out of the file; `Deploy/Instalar-Gregal.ps1` writes a new connection string straight into `appsettings.json` on-site, the same plaintext-file pattern.

The connection string also sets `Charset=ISO8859_1` explicitly (added 2026-10-15), matching the database's actual charset (confirmed via `RDB$CHARACTER_SET_NAME`) — without it, the Firebird client defaults to `NONE`, which doesn't translate accented characters/`ñ` and can corrupt text fields on write or read (surfaced when opening the database in a third-party tool, which warned about it explicitly; verified by round-tripping accented text through `HISTORICO.RECLAMACION`).

### Startup banner and PC diagnostics

`Program.cs`'s `MostrarBannerDeArranque` (console-only, skipped under `WindowsServiceHelpers.IsWindowsService()`) prints which protocol(s) are active, their ports/sala config, and warns if something's missing or a configured `ComPort` isn't visible on this machine. `Diagnostics/InfoSistema.cs` supplies the RAM/disk-free numbers (via a `GlobalMemoryStatusEx` P/Invoke) — a quick sanity check before deploying to a sala's PC.

### Deployment

`Deploy/Instalar-Gregal.ps1` installs Gregal on a new sala's PC from a published folder: checks for the .NET 10 runtime, prompts for network/serial/DB settings, rewrites `appsettings.json` in place (regex-based, preserving its `//` comments rather than a JSON round-trip), registers and starts the Windows Service, opens the TCP firewall port, and tails the resulting log. `Gregal/Docs/Migraciones/` holds hand-applied Firebird schema migrations (e.g. widening `HISTORICO.BOLAS` to `VARCHAR(300)`, since the tag protocol's long ball lists overflowed the original `VARCHAR(255)`) — these are **not** applied automatically and must be run manually against any already-deployed database.

### Logging

Serilog, configured entirely in `Program.cs` (not appsettings-only): console + daily rolling file under `<exe dir>/logs/`, path derived from `GeneralSettings.LogPath`. Note: `appsettings.json`'s `"Logging":{"LogLevel":"Debug"}` has no effect on Serilog — `ReadFrom.Configuration` only looks for a `"Serilog"` section, which doesn't exist here, so the effective minimum level is Serilog's own default (Information) regardless of what `"Logging"` says.

### Protocol reference

Full wire-format spec for the original JSON/CSV protocol (fields, examples, error codes, reconnect/heartbeat semantics) lives in `Gregal/Docs/Specs.md` and `Gregal/Docs/Dev_Anotaciones.txt` — read these before changing anything in `Protocol/` or the parsers, since external bingo-hall clients depend on this contract. The tag event-stream protocol has no written spec in this repo; its behavior is inferred from external reference classes (`Comunicaciones.cs`, `Mensaje.cs`, `CadenaEstado.cs` — not part of Gregal) and a single real example game captured in `Gregal.Tests/PartidaAccumulatorTests.cs`. Treat that test as the source of truth, and expect some less-common field/premio-code mappings to need correction once validated against real hardware in a sala.
