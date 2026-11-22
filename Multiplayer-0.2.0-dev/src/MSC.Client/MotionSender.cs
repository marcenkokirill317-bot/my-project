using System.Globalization;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // POS2: не чаще 15 Гц, только при реальном изменении + heartbeat 0.5 с.
    public sealed class MotionSender
    {
        private const float Interval = 0.066f, Heartbeat = 0.5f;
        private ushort seq;
        private float nextSend, lastSend, prevTime, lastYaw, lastPitch;
        private Vector3 prevPos, velocity, lastPos;
        private MoveState lastState;
        private bool has;

        public MoveState CurrentState { get { return lastState; } }
        public float CurrentSpeed { get { return new Vector3(velocity.x, 0f, velocity.z).magnitude; } }

        public bool TryPacket(float now, Vector3 pos, float yaw, float pitch, out string packet)
        {
            packet = null;
            if (!has) { prevPos = pos; prevTime = now; lastPos = pos; lastYaw = yaw; lastPitch = pitch; has = true; }
            float dt = now - prevTime;
            if (dt > 0.001f)
            {
                velocity = Vector3.Lerp(velocity, (pos - prevPos) / dt, 0.35f);
                prevPos = pos; prevTime = now;
            }
            MoveState state = Classify(velocity);
            bool changed = (pos - lastPos).sqrMagnitude > 0.0001f
                        || Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.5f
                        || Mathf.Abs(Mathf.DeltaAngle(pitch, lastPitch)) > 0.5f
                        || state != lastState;
            if (now < nextSend) return false;
            if (!changed && now - lastSend < Heartbeat) return false;

            nextSend = now + Interval; lastSend = now;
            lastPos = pos; lastYaw = yaw; lastPitch = pitch; lastState = state;
            unchecked { seq++; }
            packet = string.Format(CultureInfo.InvariantCulture,
                "MSC1|POS2|{0}|{1:0.###}|{2:0.###}|{3:0.###}|{4:0.#}|{5:0.#}|{6}|{7:0.##}|{8:0.##}|{9:0.##}|{10}",
                seq, pos.x, pos.y, pos.z, Normalize(yaw), Mathf.Clamp(pitch, -90f, 90f),
                (byte)state, velocity.x, velocity.y, velocity.z, (uint)(now * 1000f));
            return true;
        }

        public void Reset() { has = false; seq = 0; velocity = Vector3.zero; nextSend = 0f; lastSend = 0f; lastState = MoveState.Idle; }
        private static float Normalize(float a) { a %= 360f; return a < 0f ? a + 360f : a; }

        // Эвристика по скорости; позже заменяется данными игры.
        private static MoveState Classify(Vector3 v)
        {
            float h = new Vector3(v.x, 0f, v.z).magnitude;
            if (v.y > 1.2f) return MoveState.Jumping;
            if (v.y < -1.8f) return MoveState.Falling;
            if (h > 2.6f) return MoveState.Running;
            if (h > 0.25f) return MoveState.Walking;
            return MoveState.Idle;
        }
    }
}