using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MSC.Server;

// Конфигурация: server.json рядом с exe или в рабочем каталоге.
internal sealed class ServerConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 27020;
    public int MaxPlayers { get; set; } = 4;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxPacketsPerSecond { get; set; } = 50;
}

internal sealed class Player
{
    public required string Id { get; init; }
    public required string Nick { get; init; }
    public required IPEndPoint EndPoint { get; init; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastPoseUtc { get; set; } = DateTime.MinValue;
    public float X, Y, Z, Rot;
    public float Pitch, Vx, Vy, Vz;
    public byte MoveState;
    public ushort Seq;
    public uint SenderMs;
    public bool Legacy = true;          // клиент шлёт POS (0.1.x)
    public bool HasPosition;

    public string State(string kind) => string.Join("|", "MSC1", kind, Id, Safe(Nick), F(X), F(Y), F(Z), F(Rot));

    // MSC1|ST2|id|seq|x|y|z|yaw|pitch|state|vx|vy|vz|tms   (14 полей)
    public string State2() => string.Join("|", "MSC1", "ST2", Id,
        Seq.ToString(CultureInfo.InvariantCulture), F(X), F(Y), F(Z), D(Rot), D(Pitch),
        MoveState.ToString(CultureInfo.InvariantCulture), V(Vx), V(Vy), V(Vz),
        SenderMs.ToString(CultureInfo.InvariantCulture));

    private static string Safe(string s) => s.Replace("|", "_").Replace("\r", "_").Replace("\n", "_");
    private static string F(float n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private static string D(float n) => n.ToString("0.#", CultureInfo.InvariantCulture);
    private static string V(float n) => n.ToString("0.##", CultureInfo.InvariantCulture);
}

internal static class Program
{
    private const string Version = "0.2.0-dev";
    private const int DefaultPort = 27020;
    private static ServerConfig Cfg = new();
    private static UdpClient Udp = null!;
    private static readonly Dictionary<string, Player> Players = new();
    private static bool running = true;

    private static void Main(string[] args)
    {
        Cfg = LoadConfig();
        int port = ReadPort(args, Cfg.Port);
        try { Udp = new UdpClient(port); }
        catch (Exception ex) { Console.WriteLine("[FATAL] UDP " + port + ": " + ex.Message); return; }

        Console.WriteLine($"MSC Multiplayer {Version}");
        Console.WriteLine($"UDP {port} | max players {Cfg.MaxPlayers} | timeout {Cfg.TimeoutSeconds}s | rate {Cfg.MaxPacketsPerSecond}/s | Ctrl+C to stop");
        Udp.Client.ReceiveTimeout = 1000;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; running = false; Udp.Close(); };

        while (running)
        {
            try
            {
                IPEndPoint remote = new(IPAddress.Any, 0);
                var data = Udp.Receive(ref remote);
                Handle(Encoding.UTF8.GetString(data), remote);
            }
            catch (SocketException) when (running) { }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Console.WriteLine("[WARN] " + ex.Message); }
            RemoveTimedOut();
        }
        try { Broadcast("MSC1|ERROR|server_shutdown"); } catch { }
        Udp.Dispose();
        Console.WriteLine("Stopped.");
    }

    private static ServerConfig LoadConfig()
    {
        var cfg = new ServerConfig();
        string[] paths =
        {
            Path.Combine(AppContext.BaseDirectory, "server.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "server.json")
        };
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var loaded = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    IncludeFields = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                if (loaded == null) continue;
                if (loaded.Port is >= 1 and <= 65535) cfg.Port = loaded.Port;
                if (loaded.MaxPlayers is >= 1 and <= 16) cfg.MaxPlayers = loaded.MaxPlayers;
                if (loaded.TimeoutSeconds is >= 5 and <= 300) cfg.TimeoutSeconds = loaded.TimeoutSeconds;
                if (loaded.MaxPacketsPerSecond is >= 5 and <= 200) cfg.MaxPacketsPerSecond = loaded.MaxPacketsPerSecond;
                Console.WriteLine("[CFG] " + path);
                return cfg;
            }
            catch (Exception ex) { Console.WriteLine("[CFG] " + path + ": " + ex.Message); }
        }
        Console.WriteLine("[CFG] server.json not found, defaults in use");
        return cfg;
    }

    private static int ReadPort(string[] args, int fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--port" && int.TryParse(args[i + 1], out int value) && value >= 1 && value <= 65535)
                return value;
        return fallback is >= 1 and <= 65535 ? fallback : DefaultPort;
    }

    private static void Handle(string packet, IPEndPoint ep)
    {
        if (packet.Length > 512) return;
        var p = packet.Split('|');
        if (p.Length < 2 || p[0] != "MSC1") return;
        var current = Players.Values.FirstOrDefault(x => x.EndPoint.Equals(ep));
        if (current != null) current.LastSeenUtc = DateTime.UtcNow;

        switch (p[1])
        {
            case "HELLO": if (p.Length >= 3) Join(p[2], ep); break;
            case "PING": Send("MSC1|PONG", ep); break;
            case "BYE": if (current != null) Remove(current, "disconnect"); break;
            case "POS": ApplyLegacy(current, p); break;
            case "POS2": ApplyMotion(current, p); break;
        }
    }

    private static bool RateOk(Player p)
    {
        double minMs = 1000.0 / Cfg.MaxPacketsPerSecond;
        if ((DateTime.UtcNow - p.LastPoseUtc).TotalMilliseconds < minMs) return false;
        p.LastPoseUtc = DateTime.UtcNow;
        return true;
    }

    private static void ApplyLegacy(Player? current, string[] p)
    {
        if (current == null || p.Length != 6 || !RateOk(current)) return;
        if (!Try(p[2], out var x) || !Try(p[3], out var y) ||
            !Try(p[4], out var z) || !Try(p[5], out var r)) return;
        if (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000 || Math.Abs(r) > 1000000) return;

        current.X = x; current.Y = y; current.Z = z; current.Rot = Normalize(r);
        current.Pitch = 0f; current.Vx = current.Vy = current.Vz = 0f;
        current.MoveState = 0; current.SenderMs = 0;
        current.Legacy = true;
        current.Seq = unchecked((ushort)(current.Seq + 1));
        current.HasPosition = true;

        Broadcast(current.State("STATE"), current.EndPoint);
        if (Players.Values.Any(q => !q.Legacy)) Broadcast(current.State2(), current.EndPoint);
    }

    private static void ApplyMotion(Player? current, string[] p)
    {
        // MSC1|POS2|seq|x|y|z|yaw|pitch|state|vx|vy|vz|tms   (13 полей)
        if (current == null || p.Length != 13 || !RateOk(current)) return;
        if (!ushort.TryParse(p[2], out ushort seq)) return;
        if (!Try(p[3], out var x) || !Try(p[4], out var y) || !Try(p[5], out var z)) return;
        if (!Try(p[6], out var yaw) || !Try(p[7], out var pitch)) return;
        if (!int.TryParse(p[8], out int state) || state < 0 || state > 4) return;
        if (!Try(p[9], out var vx) || !Try(p[10], out var vy) || !Try(p[11], out var vz)) return;
        if (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000) return;
        uint.TryParse(p[12], out uint tms);

        if (current.HasPosition && !current.Legacy && !Newer(seq, current.Seq)) return;
        if (vx * vx + vy * vy + vz * vz > 3600f) { vx = vy = vz = 0f; }

        current.X = x; current.Y = y; current.Z = z;
        current.Rot = Normalize(yaw);
        current.Pitch = Math.Clamp(pitch, -90f, 90f);
        current.Vx = vx; current.Vy = vy; current.Vz = vz;
        current.MoveState = (byte)state;
        current.SenderMs = tms;
        current.Seq = seq;
        current.Legacy = false;
        current.HasPosition = true;

        Broadcast(current.State2(), current.EndPoint);
        if (Players.Values.Any(q => q.Legacy)) Broadcast(current.State("STATE"), current.EndPoint);
    }

    private static bool Newer(ushort a, ushort b) => a != b && unchecked((ushort)(a - b)) < 32768;
    private static float Normalize(float a) { a %= 360f; return a < 0f ? a + 360f : a; }

    private static void Join(string raw, IPEndPoint ep)
    {
        var existing = Players.Values.FirstOrDefault(x => x.EndPoint.Equals(ep));
        if (existing != null) { existing.LastSeenUtc = DateTime.UtcNow; Send($"MSC1|WELCOME|{existing.Id}|{Cfg.MaxPlayers}", ep); return; }
        if (Players.Count >= Cfg.MaxPlayers) { Send("MSC1|ERROR|server_full", ep); return; }
        var nick = new string(raw.Where(c => !char.IsControl(c) && c != '|').Take(20).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(nick)) nick = "Player";
        foreach (var other in Players.Values)
        {
            Send($"MSC1|PLAYER_JOIN|{other.Id}|{other.Nick}", ep);
            if (other.HasPosition) { Send(other.State("ROSTER"), ep); Send(other.State2(), ep); }
        }
        var player = new Player { Id = Guid.NewGuid().ToString("N")[..8], Nick = nick, EndPoint = ep };
        Players.Add(player.Id, player);
        Send($"MSC1|WELCOME|{player.Id}|{Cfg.MaxPlayers}", ep);
        Broadcast($"MSC1|PLAYER_JOIN|{player.Id}|{nick}", ep);
        Console.WriteLine($"[JOIN] {nick} {ep} ({Players.Count}/{Cfg.MaxPlayers})");
    }

    private static void RemoveTimedOut()
    {
        foreach (var p in Players.Values.Where(x => DateTime.UtcNow - x.LastSeenUtc > TimeSpan.FromSeconds(Cfg.TimeoutSeconds)).ToArray())
            Remove(p, "timeout");
    }

    private static void Remove(Player p, string reason)
    {
        Players.Remove(p.Id);
        Broadcast($"MSC1|PLAYER_LEAVE|{p.Id}");
        Console.WriteLine($"[LEAVE] {p.Nick}: {reason} ({Players.Count} left)");
    }

    private static void Broadcast(string s, IPEndPoint? except = null)
    {
        var b = Encoding.UTF8.GetBytes(s);
        foreach (var p in Players.Values.ToArray())
            if (except == null || !p.EndPoint.Equals(except))
                try { Udp.Send(b, b.Length, p.EndPoint); } catch { }
    }

    private static void Send(string s, IPEndPoint ep)
    {
        var b = Encoding.UTF8.GetBytes(s);
        try { Udp.Send(b, b.Length, ep); } catch { }
    }

    private static bool Try(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) &&
        !float.IsNaN(v) && !float.IsInfinity(v);
}