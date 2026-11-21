using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MSC.Server;

internal sealed class Player
{
    public required string Id { get; init; }
    public required string Nick { get; init; }
    public required IPEndPoint EndPoint { get; init; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public float X, Y, Z, Rot;
    public bool HasPosition;

    public string State(string kind) => string.Join("|", "MSC1", kind, Id, Safe(Nick), F(X), F(Y), F(Z), F(Rot));
    private static string Safe(string s) => s.Replace("|", "_").Replace("\r", "_").Replace("\n", "_");
    private static string F(float n) => n.ToString("0.###", CultureInfo.InvariantCulture);
}

internal static class Program
{
    private const int DefaultPort = 27020, MaxPlayers = 4;
    private static UdpClient Udp = null!;
    private static int Port = DefaultPort;
    private static readonly Dictionary<string, Player> Players = new();
    private static bool running = true;

    private static void Main(string[] args)
    {
        Port = ReadPort(args);
        Udp = new UdpClient(Port);
        Console.WriteLine("MSC Multiplayer 0.1.8-dev");
        Console.WriteLine($"UDP {Port} | max players {MaxPlayers} | Ctrl+C to stop");
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
        Udp.Dispose();
        Console.WriteLine("Stopped.");
    }

    private static int ReadPort(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--port" && int.TryParse(args[i + 1], out int value) && value >= 1 && value <= 65535)
                return value;
        return DefaultPort;
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
            case "HELLO":
                if (p.Length >= 3) Join(p[2], ep);
                break;
            case "PING": Send("MSC1|PONG", ep); break;
            case "BYE":
                if (current != null) Remove(current, "disconnect");
                break;
            case "POS":
                if (current == null || p.Length != 6) return;
                if (!Try(p[2], out var x) || !Try(p[3], out var y) ||
                    !Try(p[4], out var z) || !Try(p[5], out var r)) return;
                if (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000 || Math.Abs(r) > 1000000) return;
                current.X=x; current.Y=y; current.Z=z; current.Rot=r; current.HasPosition=true;
                Broadcast(current.State("STATE"), current.EndPoint);
                break;
        }
    }

    private static void Join(string raw, IPEndPoint ep)
    {
        var existing = Players.Values.FirstOrDefault(x => x.EndPoint.Equals(ep));
        if (existing != null) { existing.LastSeenUtc=DateTime.UtcNow; Send($"MSC1|WELCOME|{existing.Id}|{MaxPlayers}", ep); return; }
        if (Players.Count >= MaxPlayers) { Send("MSC1|ERROR|server_full", ep); return; }
        var nick = new string(raw.Where(c => !char.IsControl(c) && c != '|').Take(20).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(nick)) nick = "Player";
        foreach (var other in Players.Values)
        {
            Send($"MSC1|PLAYER_JOIN|{other.Id}|{other.Nick}", ep);
            if (other.HasPosition) Send(other.State("ROSTER"), ep);
        }
        var player = new Player { Id=Guid.NewGuid().ToString("N")[..8], Nick=nick, EndPoint=ep };
        Players.Add(player.Id, player);
        Send($"MSC1|WELCOME|{player.Id}|{MaxPlayers}", ep);
        Broadcast($"MSC1|PLAYER_JOIN|{player.Id}|{nick}", ep);
        Console.WriteLine($"[JOIN] {nick} {ep}");
    }

    private static void RemoveTimedOut()
    {
        foreach (var p in Players.Values.Where(x => DateTime.UtcNow-x.LastSeenUtc > TimeSpan.FromSeconds(30)).ToArray())
            Remove(p, "timeout");
    }
    private static void Remove(Player p, string reason)
    {
        Players.Remove(p.Id);
        Broadcast($"MSC1|PLAYER_LEAVE|{p.Id}");
        Console.WriteLine($"[LEAVE] {p.Nick}: {reason}");
    }
    private static void Broadcast(string s, IPEndPoint? except = null)
    {
        var b=Encoding.UTF8.GetBytes(s);
        foreach (var p in Players.Values)
            if (except == null || !p.EndPoint.Equals(except))
                try { Udp.Send(b,b.Length,p.EndPoint); } catch { }
    }
    private static void Send(string s, IPEndPoint ep)
    {
        var b=Encoding.UTF8.GetBytes(s);
        try { Udp.Send(b,b.Length,ep); } catch { }
    }
    private static bool Try(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) &&
        !float.IsNaN(v) && !float.IsInfinity(v);
}
