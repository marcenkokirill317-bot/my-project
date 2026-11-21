using System;
using System.Collections.Generic;
using System.Globalization;

namespace MSC.Multiplayer.Client
{
    public sealed class RemotePlayerState
    {
        public string Id, Nickname;
        public float X, Y, Z, RotationY;
        public DateTime ReceivedUtc;
        public bool HasPosition;
    }

    public sealed class PlayerSyncManager
    {
        private readonly Dictionary<string, RemotePlayerState> players = new Dictionary<string, RemotePlayerState>();
        public IEnumerable<RemotePlayerState> Players { get { return players.Values; } }
        public int Count { get { return players.Count; } }
        public void Clear() { players.Clear(); }

        public void Handle(string packet)
        {
            if (String.IsNullOrEmpty(packet)) return;
            string[] p = packet.Split('|');
            if (p.Length < 3 || p[0] != "MSC1") return;
            if (p[1] == "PLAYER_LEAVE") { players.Remove(p[2]); return; }

            // PLAYER_JOIN нужен для немедленного обновления списка лобби у уже подключённых игроков.
            if (p[1] == "PLAYER_JOIN")
            {
                if (p.Length < 4) return;
                RemotePlayerState joined;
                if (!players.TryGetValue(p[2], out joined))
                    players[p[2]] = new RemotePlayerState { Id = p[2], Nickname = p[3], ReceivedUtc = DateTime.UtcNow, HasPosition = false };
                else joined.Nickname = p[3];
                return;
            }

            if (p[1] != "STATE" && p[1] != "ROSTER") return;
            if (p.Length != 8) return;
            float x, y, z, r;
            if (!Parse(p[4], out x) || !Parse(p[5], out y) || !Parse(p[6], out z) || !Parse(p[7], out r)) return;
            if (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000 || Math.Abs(r) > 1000000) return;
            players[p[2]] = new RemotePlayerState
            {
                Id = p[2], Nickname = p[3], X = x, Y = y, Z = z,
                RotationY = r, ReceivedUtc = DateTime.UtcNow, HasPosition = true
            };
        }

        private static bool Parse(string s, out float f)
        {
            return Single.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !Single.IsNaN(f) && !Single.IsInfinity(f);
        }
    }
}
