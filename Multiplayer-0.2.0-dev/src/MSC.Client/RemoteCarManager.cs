using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // Машины удалённых игроков. Данные из CarCodec, интерполяция в CarMotion.
    public sealed class RemoteCarManager
    {
        private sealed class Entry
        {
            public ICarModel Model;
            public RemoteMotion Motion;     // переиспользуем буфер: позиция/поворот/скорость
        }

        private readonly Dictionary<string, Entry> cars = new Dictionary<string, Entry>();
        private readonly HashSet<string> alive = new HashSet<string>();
        private readonly List<string> remove = new List<string>();
        public int Count { get { return cars.Count; } }

        public void HandlePacket(string id, CarSnapshot snap)
        {
            Entry e;
            if (!cars.TryGetValue(id, out e))
            {
                ICarModel model = new PlaceholderCarModel(id, HumanoidAvatar.ColorFor("car" + id));
                e = new Entry { Model = model, Motion = new RemoteMotion() };
                cars.Add(id, e);
            }
            e.Motion.Push(new MotionSnapshot
            {
                Seq = snap.Seq,
                Position = snap.Position,
                Yaw = snap.Yaw,
                Velocity = snap.Velocity,
                State = MoveState.Idle,
                ReceivedAt = snap.ReceivedAt
            });
        }

        public void Update(float deltaTime)
        {
            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<string, Entry> pair in cars)
            {
                Entry e = pair.Value;
                e.Motion.Sample(now);
                if (!e.Motion.HasData) continue;
                try { e.Model.Tick(e.Motion.Position, e.Motion.Yaw, e.Motion.Speed); }
                catch { }
            }
        }

        public void RemovePlayer(string id)
        {
            Entry e;
            if (cars.TryGetValue(id, out e))
            {
                try { e.Model.Dispose(); } catch { }
                cars.Remove(id);
            }
        }

        public void Clear()
        {
            foreach (Entry e in cars.Values) { try { e.Model.Dispose(); } catch { } }
            cars.Clear();
        }
    }
}