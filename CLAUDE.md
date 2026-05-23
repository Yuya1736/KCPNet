# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

Build everything from repo root:
- `dotnet build`

Run the server:
- `dotnet run --project KCPExampleServer`

Run the client:
- `dotnet run --project KCPExampleClient`

Build library only:
- `dotnet build KCPNet/KCPNet.csproj`

Both example projects target `net8.0`. The core library targets `netstandard2.0`.

## Architecture

**Transport layer** (`KCPNet.cs`): Generic class `KCPNet<T, K>` where `T : KCPSession<K>, new()` and `K : KCPMessage, new()`. Runs a UDP receive loop via `Task.Run`. Server assigns unique `uint` session IDs; client connects by sending 4 zero bytes and receiving an 8-byte handshake reply (first 4 bytes zero, last 4 bytes = session ID). All subsequent UDP payloads are prefixed with the 4-byte session ID. Uses `IOControl(SIO_UDP_CONNRESET)` on Windows only to suppress ICMP port-unreachable exceptions.

**Session layer** (`KCPSession.cs`): Per-peer state holding a `Kcp<KcpSegment>` instance in fast mode (`NoDelay(1, 10, 2, 1)`, WndSize 64, MTU 512). Runs a background `async void Update()` loop (10ms tick) that calls `_kcp.Update()` and drains received data via `_kcp.PeekSize()`/`_kcp.Recv()`. All `_kcp` access is guarded by `_kcpLock`. Subclasses override `OnReceiveMessage`, `OnUpdate`, `OnConnected`, `OnDisconnected`.

**Message pipeline**: `SendMessage(T)` → MessagePack serialize → GZip compress → `_kcp.Send()` → `KCPHandle.Out` callback → UDP send. Receive is the reverse: UDP → `_kcp.Input()` → `_kcp.Recv()` → GZip decompress → MessagePack deserialize → `OnReceiveMessage`.

**Ping/timeout** (`ServerSession.cs`): `OnUpdate` increments a counter every 5s; after 3 missed pings (15s total) it calls `CloseSession()`. Client resets the counter on each received ping reply.

## Key Notes

- All message types must be decorated with `[MessagePackObject]` and fields with `[Key(n)]`. `KCPMessage` itself carries `[MessagePackObject]`.
- `Update()` in `KCPSession.cs` and `SendUdpMessage()` in `KCPNet.cs` are `async void` — exceptions inside them won't propagate to callers.
- `BroadcastMessage` iterates `_sessionDic` without locking — safe only if called from the same thread that modifies the dictionary, or if no sessions are being added/removed concurrently.
- NuGet dependencies: `Kcp` v2.7.0 (`System.Net.Sockets.Kcp`), `MessagePack` v2.5.187.
- `KCPTool` logging delegates (`LogFunc`, `ColorLogFunc`, `WarnFunc`, `ErrorFunc`) are `null` by default and fall back to colored console output. Replace them to integrate with Unity's `Debug.Log` or any other logging system.
