using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // Жизненный цикл удалённых персонажей: данные из PlayerSyncManager, вид из IAvatarModel,
    // ник из Nametag. Протокола здесь нет.
    public sealed class RemoteAvatarManager
    {
        public static float VerticalOffset = 0f;   // подстройка высоты модели, метры

        private sealed class Entry
        {
            public IAvatarModel Model;
            public NametagAnchor Anchor;
            public string Nickname;
        }

        private readonly Dictionary<string, Entry> avatars = new Dictionary<string, Entry>();
        private readonly HashSet<string> alive = new HashSet<string>();
        private readonly List<string> remove = new List<string>();
        private Transform localPlayer;
        public string LastDiagnostic;
        public int Count { get { return avatars.Count; } }

        public void SetLocalPlayer(Transform t) { localPlayer = t; }

        public void Update(PlayerSyncManager sync, float deltaTime)
        {
            if (sync == null) return;
            alive.Clear();
            float now = Time.realtimeSinceStartup;

            foreach (RemotePlayerState state in sync.Players)
            {
                alive.Add(state.Id);
                if (!state.HasPosition) continue;

                Entry e;
                if (!avatars.TryGetValue(state.Id, out e))
                {
                    try
                    {
                        string diag;
                        IAvatarModel model = AvatarFactory.Create(state.Id, localPlayer, out diag);
                        if (diag != null) LastDiagnostic = diag;
                        if (model == null || model.Root == null) continue;
                        e = new Entry { Model = model, Anchor = new NametagAnchor(), Nickname = state.Nickname };
                        e.Anchor.Bind(model.Root, model.HeadAnchor);
                        avatars.Add(state.Id, e);
                    }
                    catch (Exception ex) { LastDiagnostic = "avatar create failed: " + ex.Message; continue; }
                }

                e.Nickname = state.Nickname;
                state.Motion.Sample(now);
                Vector3 pos = state.Motion.HasData ? state.Motion.Position : new Vector3(state.X, state.Y, state.Z);
                pos.y += VerticalOffset;
                float yaw = state.Motion.HasData ? state.Motion.Yaw : state.RotationY;
                try { e.Model.Tick(pos, yaw, state.Motion.Pitch, state.Motion.State, state.Motion.Speed, deltaTime); }
                catch (Exception ex) { LastDiagnostic = "avatar tick failed: " + ex.Message; }
            }

            remove.Clear();
            foreach (KeyValuePair<string, Entry> pair in avatars)
                if (!alive.Contains(pair.Key)) remove.Add(pair.Key);
            for (int i = 0; i < remove.Count; i++)
            {
                try { avatars[remove[i]].Model.Dispose(); } catch { }
                avatars.Remove(remove[i]);
            }
        }

        public void DrawNames(Camera camera)
        {
            if (camera == null) return;
            foreach (Entry e in avatars.Values)
            {
                if (e.Model == null || e.Model.Root == null) continue;
                Nametag.Draw(camera, e.Anchor.Position, e.Nickname);
            }
        }

        public void Clear()
        {
            foreach (Entry e in avatars.Values) { try { e.Model.Dispose(); } catch { } }
            avatars.Clear();
        }
    }
}