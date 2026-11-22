using System;
using System.Globalization;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    public struct CarSnapshot
    {
        public ushort Seq;
        public Vector3 Position;
        public float Yaw;
        public Vector3 Velocity;
        public bool DriverPresent;
        public float ReceivedAt;
    }

    // Формирует CAR: 15 Гц, только при изменении.
    public sealed class CarSender
    {
        private const float Interval = 0.066f, Heartbeat = 0.5f;
        private ushort seq;
        private float nextSend, lastSend, lastYaw;
        private Vector3 lastPos;
        private bool lastDriver, has;

        public bool TryPacket(float now, Vector3 pos, float yaw, Vector3 velocity, bool driver, out string packet)
        {
            packet = null;
            if (!has) { lastPos = pos; lastYaw = yaw; lastDriver = driver; has = true; }
            bool changed = (pos - lastPos).sqrMagnitude > 0.0004f
                        || Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.5f
                        || driver != lastDriver;
            if (now < nextSend) return false;
            if (!changed && now - lastSend < Heartbeat) return false;

            nextSend = now + Interval; lastSend = now;
            lastPos = pos; lastYaw = yaw; lastDriver = driver;
            unchecked { seq++; }
            packet = string.Format(CultureInfo.InvariantCulture,
                "MSC1|CAR|{0}|{1:0.###}|{2:0.###}|{3:0.###}|{4:0.#}|{5:0.##}|{6:0.##}|{7:0.##}|{8}",
                seq, pos.x, pos.y, pos.z, Normalize(yaw), velocity.x, velocity.y, velocity.z, driver ? 1 : 0);
            return true;
        }

        public void Reset() { has = false; seq = 0; nextSend = 0f; lastSend = 0f; }
        private static float Normalize(float a) { a %= 360f; return a < 0f ? a + 360f : a; }
    }

    public static class CarCodec
    {
        // CS: MSC1|CS|id|seq|x|y|z|yaw|vx|vy|vz|driver   (12 полей)
        public static bool TryParseCS(string[] f, float receivedAt, out string id, out CarSnapshot s)
        {
            id = null; s = default(CarSnapshot);
            if (f == null || f.Length != 12 || f[0] != "MSC1" || f[1] != "CS") return false;
            ushort seq; float x, y, z, yaw, vx, vy, vz; int driver;
            if (!ushort.TryParse(f[3], out seq)) return false;
            if (!Num(f[4], out x) || !Num(f[5], out y) || !Num(f[6], out z)) return false;
            if (!Num(f[7], out yaw)) return false;
            if (!Num(f[8], out vx) || !Num(f[9], out vy) || !Num(f[10], out vz)) return false;
            if (!int.TryParse(f[11], out driver)) return false;
            id = f[2];
            s = new CarSnapshot
            {
                Seq = seq, Position = new Vector3(x, y, z), Yaw = yaw,
                Velocity = new Vector3(vx, vy, vz), DriverPresent = driver != 0, ReceivedAt = receivedAt
            };
            return true;
        }

        private static bool Num(string text, out float v)
        {
            if (!Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            if (Single.IsNaN(v) || Single.IsInfinity(v)) return false;
            return Math.Abs(v) <= 100000f;
        }
    }
}