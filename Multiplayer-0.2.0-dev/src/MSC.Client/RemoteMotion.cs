using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    public enum MoveState : byte { Idle = 0, Walking = 1, Running = 2, Jumping = 3, Falling = 4 }

    public struct MotionSnapshot
    {
        public ushort Seq;
        public Vector3 Position;
        public float Yaw, Pitch;
        public Vector3 Velocity;
        public MoveState State;
        public float ReceivedAt;      // локальное время приёма
        public uint SenderTimeMs;     // время отправителя, только для диагностики
    }

    // Буфер снимков: защита от устаревших пакетов, интерполяция с задержкой, короткая экстраполяция.
    public sealed class RemoteMotion
    {
        private const int Capacity = 24;
        private const float InterpDelay = 0.12f;
        private const float MaxExtrapolation = 0.25f;
        private const float TeleportDistance = 6f;

        private readonly List<MotionSnapshot> shots = new List<MotionSnapshot>(Capacity);
        private ushort lastSeq;
        private bool hasSeq;

        public Vector3 Position { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public MoveState State { get; private set; }
        public float Speed { get; private set; }
        public bool HasData { get; private set; }
        public int Buffered { get { return shots.Count; } }

        private static bool Newer(ushort a, ushort b) { return a != b && unchecked((ushort)(a - b)) < 32768; }
        private static float Horizontal(Vector3 v) { return new Vector3(v.x, 0f, v.z).magnitude; }

        public void Push(MotionSnapshot s)
        {
            if (hasSeq && !Newer(s.Seq, lastSeq)) return;
            Append(s); lastSeq = s.Seq; hasSeq = true;
        }

        public void PushUnsequenced(MotionSnapshot s)   // legacy STATE без seq
        {
            s.Seq = hasSeq ? unchecked((ushort)(lastSeq + 1)) : (ushort)1;
            Append(s); lastSeq = s.Seq; hasSeq = true;
        }

        private void Append(MotionSnapshot s)
        {
            if (shots.Count > 0)
            {
                MotionSnapshot prev = shots[shots.Count - 1];
                if ((s.Position - prev.Position).sqrMagnitude > TeleportDistance * TeleportDistance)
                {
                    // Телепорт: сбрасываем буфер и НЕ выводим скорость из скачка,
                    // иначе экстраполяция утащит аватар на сотни метров.
                    shots.Clear();
                    s.Velocity = Vector3.zero;
                }
                else
                {
                    float dt = s.ReceivedAt - prev.ReceivedAt;
                    if (s.Velocity == Vector3.zero && dt > 0.001f)
                    {
                        Vector3 v = (s.Position - prev.Position) / dt;
                        s.Velocity = v.sqrMagnitude <= 625f ? v : Vector3.zero;   // >25 м/с — мусор
                    }
                }
            }
            shots.Add(s);
            if (shots.Count > Capacity) shots.RemoveAt(0);
            if (!HasData) { Position = s.Position; Yaw = s.Yaw; Pitch = s.Pitch; State = s.State; HasData = true; }
        }

        public void Sample(float now)
        {
            if (shots.Count == 0) return;
            float t = now - InterpDelay;
            int hi = -1;
            for (int i = 0; i < shots.Count; i++) if (shots[i].ReceivedAt >= t) { hi = i; break; }

            if (hi < 0)
            {
                MotionSnapshot last = shots[shots.Count - 1];
                float ahead = Mathf.Min(now - last.ReceivedAt, MaxExtrapolation);
                Position = last.Position + last.Velocity * ahead;
                Yaw = last.Yaw; Pitch = last.Pitch; State = last.State; Speed = Horizontal(last.Velocity);
                return;
            }
            if (hi == 0)
            {
                MotionSnapshot f = shots[0];
                Position = f.Position; Yaw = f.Yaw; Pitch = f.Pitch; State = f.State; Speed = Horizontal(f.Velocity);
                return;
            }
            MotionSnapshot a = shots[hi - 1], b = shots[hi];
            float span = b.ReceivedAt - a.ReceivedAt;
            float k = span > 0.0001f ? Mathf.Clamp01((t - a.ReceivedAt) / span) : 1f;
            Position = Vector3.Lerp(a.Position, b.Position, k);
            Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, k);
            Pitch = Mathf.LerpAngle(a.Pitch, b.Pitch, k);
            State = b.State;
            Speed = Horizontal(Vector3.Lerp(a.Velocity, b.Velocity, k));
        }

        public void Reset() { shots.Clear(); hasSeq = false; HasData = false; Speed = 0f; State = MoveState.Idle; }
    }

    public static class MotionCodec
    {
        // ST2:   MSC1|ST2|id|seq|x|y|z|yaw|pitch|state|vx|vy|vz|tms   (14 полей)
        // STATE: MSC1|STATE|id|nick|x|y|z|rot                        (8 полей)
        public static bool TryParse(string[] f, float receivedAt, out string id, out MotionSnapshot s, out bool sequenced)
        {
            id = null; s = default(MotionSnapshot); sequenced = false;
            if (f == null || f.Length < 3 || f[0] != "MSC1") return false;

            if (f[1] == "ST2" && f.Length == 14)
            {
                ushort seq; float x, y, z, yaw, pitch, vx, vy, vz; int st; uint tms;
                if (!ushort.TryParse(f[3], out seq)) return false;
                if (!Num(f[4], out x) || !Num(f[5], out y) || !Num(f[6], out z)) return false;
                if (!Num(f[7], out yaw) || !Num(f[8], out pitch)) return false;
                if (!int.TryParse(f[9], out st) || st < 0 || st > 4) return false;
                if (!Num(f[10], out vx) || !Num(f[11], out vy) || !Num(f[12], out vz)) return false;
                uint.TryParse(f[13], out tms);
                id = f[2];
                s = new MotionSnapshot
                {
                    Seq = seq, Position = new Vector3(x, y, z), Yaw = yaw,
                    Pitch = Mathf.Clamp(pitch, -90f, 90f), Velocity = new Vector3(vx, vy, vz),
                    State = (MoveState)st, ReceivedAt = receivedAt, SenderTimeMs = tms
                };
                sequenced = true;
                return true;
            }
            if (f[1] == "STATE" && f.Length == 8)
            {
                float x, y, z, rot;
                if (!Num(f[4], out x) || !Num(f[5], out y) || !Num(f[6], out z) || !Num(f[7], out rot)) return false;
                id = f[2];
                s = new MotionSnapshot
                {
                    Seq = 0, Position = new Vector3(x, y, z), Yaw = rot, Pitch = 0f,
                    Velocity = Vector3.zero, State = MoveState.Idle, ReceivedAt = receivedAt, SenderTimeMs = 0
                };
                return true;
            }
            return false;
        }

        private static bool Num(string text, out float v)
        {
            if (!Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            if (Single.IsNaN(v) || Single.IsInfinity(v)) return false;
            return Math.Abs(v) <= 100000f;
        }
    }
}