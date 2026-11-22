using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    public sealed class RemotePlayerState
    {
        public string Id, Nickname;
        public float X, Y, Z, RotationY;
        public DateTime ReceivedUtc;
        public bool HasPosition;
        public bool HasV2;                    // получали ST2 — legacy STATE больше не двигает аватар
        public readonly RemoteMotion Motion = new RemoteMotion();
    }

    public sealed class PlayerSyncManager
    {
        private readonly Dictionary<string, RemotePlayerState> players = new Dictionary<string, RemotePlayerState>();
        public IEnumerable<RemotePlayerState> Players { get { return players.Values; } }
        public int Count { get { return players.Count; } }
        public void Clear() { players.Clear(); }

        private RemotePlayerState GetOrCreate(string id, string nick)
        {
            RemotePlayerState s;
            if (!players.TryGetValue(id, out s))
            {
                s = new RemotePlayerState { Id = id, Nickname = string.IsNullOrEmpty(nick) ? "Player" : nick, ReceivedUtc = DateTime.UtcNow };
                players[id] = s;
            }
            else if (!string.IsNullOrEmpty(nick)) s.Nickname = nick;
            return s;
        }

        public void Handle(string packet)
        {
            if (String.IsNullOrEmpty(packet)) return;
            string[] p = packet.Split('|');
            if (p.Length < 3 || p[0] != "MSC1") return;
            if (p[1] == "PLAYER_LEAVE") { players.Remove(p[2]); return; }
            if (p[1] == "PLAYER_JOIN") { if (p.Length >= 4) GetOrCreate(p[2], p[3]); return; }

            string id; MotionSnapshot snap; bool sequenced;
            if (!MotionCodec.TryParse(p, Time.realtimeSinceStartup, out id, out snap, out sequenced)) return;

            if (!sequenced)
            {
                RemotePlayerState st = GetOrCreate(id, p[3]);
                st.ReceivedUtc = DateTime.UtcNow;
                if (st.HasV2) return;         // ST2 приоритетнее legacy STATE
                st.X = snap.Position.x; st.Y = snap.Position.y; st.Z = snap.Position.z;
                st.RotationY = snap.Yaw; st.HasPosition = true;
                st.Motion.PushUnsequenced(snap);
                return;
            }
            RemotePlayerState s2 = GetOrCreate(id, null);
            s2.ReceivedUtc = DateTime.UtcNow;
            s2.Motion.Push(snap);
            s2.HasV2 = true;
            s2.X = snap.Position.x; s2.Y = snap.Position.y; s2.Z = snap.Position.z;
            s2.RotationY = snap.Yaw; s2.HasPosition = true;
        }
    }
}