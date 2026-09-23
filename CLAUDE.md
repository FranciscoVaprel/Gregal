# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Gregal is a .NET 10 Worker Service that receives bingo-hall session results ("historicos de partidas") over TCP and RS232 serial, parses them (JSON or CSV framing), and upserts them into a Firebird database. It doubles as a Windows Service (`GregalReceiverService`) in production. Solution: `Gregal.slnx` → `Gregal/Gregal.csproj` (the service) + `Gregal.Tests/Gregal.Tests.csproj` (xUnit tests).

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

### Dual-transport hosted services, no shared `Worker`

`Program.cs` registers **two independent `IHostedService` implementations** that run concurrently and share the same singletons (`GregalParserEngine`, `HistoricoRepository`):

- `Networking/TcpGregalReceiver.cs` — listens on `NetworkSettings.TcpAddress:TcpPort`, accepts multiple concurrent clients (fire-and-forget per connection), enforces an IP allow-list (`NetworkSettings.AllowedIps`). The port bind (`IPAddress.Parse`/`_server.Start()`) happens synchronously in `StartAsync` on purpose, so a bad address or an already-bound port fails host startup loudly instead of dying silently in the background accept loop.
- `Networking/SerialGregalReceiver.cs` — owns a `SerialPort` configured from `NetworkSettings`. The `DataReceived` event (fired on a ThreadPool thread) only accumulates bytes and extracts complete frames; it hands each frame to an unbounded `Channel<string>`, which a single async consumer loop (`ProcessChannelAsync`) drains with real `await`. This avoids blocking the serial event thread and avoids races on the accumulator.

There is no shared base class or common interface between the two receivers (an earlier `Worker`/`IGregalReceiver` abstraction was removed as dead code) — each is a standalone `IHostedService` registered directly in `Program.cs`.

### Message pipeline (identical shape on both transports)

```
raw bytes → string accumulator → split on delimiter → GregalParserEngine.Parse()
   → ping short-circuit, else JsonGregalParser or CsvGregalParser (IParser) → ParseResult
   → HistoricoRepository.SaveHistoricoAsync(ParseResult.Data) → GregalProtocolFactory response
```

- Framing delimiter: `\0` for TCP (hardcoded) and `NetworkSettings.SerialDelimiter` for serial (configurable, defaults to `\0`). Buffers are bounded by `NetworkSettings.TcpMaxBufferChars`; exceeding it clears the buffer and replies `BUFFER_EXCEEDED`.
- `GregalParserEngine` (Parsing/GregalParserEngine.cs) sniffs the raw string: `operation":"ping"` / literal `ping` → pong; starts with `{` → `JsonGregalParser`; anything else → `CsvGregalParser`. CSV is positional (`msg_id;operation;codSala;codConc;fecha;partidaDia;precio;venta;caja;...`), for legacy hardware that can't emit JSON.
- Responses are always a `GregalResponse` JSON object serialized with a trailing `\0` (see `GregalProtocolFactory`), even on error — the client must never be left waiting.

### Persistence

`Data/HistoricoRepository.cs` does a single Firebird `UPDATE OR INSERT ... MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)` against `HISTORICO`, inside a transaction, with the 99 `PREMIOn` columns built dynamically (`HistoricoDTO.Premios` is a `decimal?[99]`). All writes go through a process-wide `static SemaphoreSlim(1,1)` so concurrent TCP/serial inserts never overlap — if you touch this method, preserve that serialization or justify removing it.

### Configuration

Three strongly-typed options sections bound from `appsettings.json` via `IOptionsMonitor<T>` (Config/*.cs): `GeneralSettings`, `NetworkSettings` (covers both TCP and serial params — both receivers always run regardless of the `Mode` field, which is currently vestigial), `DatabaseSettings`.

The Firebird connection string in `appsettings.json` currently embeds a plaintext password; be careful not to further propagate that value (e.g. into logs, docs, or commit messages) when editing nearby code.

### Logging

Serilog, configured entirely in `Program.cs` (not appsettings-only): console + daily rolling file under `<exe dir>/logs/`, path derived from `GeneralSettings.LogPath`.

### Protocol reference

Full wire-format spec (fields, JSON/CSV examples, error codes, reconnect/heartbeat semantics) lives in `Gregal/Docs/Specs.md` and `Gregal/Docs/Dev_Anotaciones.txt` — read these before changing anything in `Protocol/` or the parsers, since external bingo-hall clients depend on this contract.
