# Changelog 0.1.7-dev

## Lobby and connection fixes
- Added connection handshake timeout and connected heartbeat timeout.
- Added explicit server rejection handling (including server full).
- Added cancellation while waiting for WELCOME.
- Added IPv4 selection for DNS hostnames and clearer unsupported IPv6 error.
- Added nickname validation and 20-character limit on client.
- Persisted host, port and nickname with PlayerPrefs.
- Added PLAYER_JOIN handling so existing clients update the lobby list immediately.
- Added lobby-only placeholder records without spawning a 3D avatar at world origin.
- Added configurable server port via `--port`.
- Updated Windows helper batch file to ASCII-only commands and CRLF line endings.

## Not verified
- Windows/.NET build and in-game tests were not run in this environment.

# Changelog 0.1.9-dev

- Receive thread now uses its own socket reference and connection generation.
- Heartbeat timestamps use atomic reads/writes.
- Stale remote player/avatar state is cleared after connection loss.
- New clients receive lobby PLAYER_JOIN records for existing players.
- ROSTER position packets are sent only after a player has sent its first POS.
- Client/server coordinate bounds are aligned.
- Added `tools/protocol_smoke_test.py` for a four-client server smoke test.

Not verified: C# compilation and in-game testing still require Windows, .NET SDK and My Summer Car.

# Changelog 0.1.9-dev

- Fixed stale lobby entries when a connection fails before WELCOME is received.
- Partial roster and avatar state is now cleared after both connected and connecting sessions terminate.

# Changelog 0.1.10-dev

- The client build menu now prompts for the My Summer Car installation root.
- Required game DLLs are checked before invoking dotnet build.
- Corrected the version label in START_HERE.bat.
- No game DLLs are bundled.
