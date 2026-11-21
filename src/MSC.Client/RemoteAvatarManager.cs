using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // Временная 3D-фигура из примитивов Unity. Позже заменяется на игровую модель игрока.
    public sealed class RemoteAvatarManager
    {
        private sealed class Avatar
        {
            public GameObject Root;
            public RemotePlayerState State;
            public Vector3 Target;
            public Quaternion TargetRotation;
        }
        private readonly Dictionary<string, Avatar> avatars = new Dictionary<string, Avatar>();
        public void Update(PlayerSyncManager sync, float deltaTime)
        {
            HashSet<string> alive = new HashSet<string>();
            foreach (RemotePlayerState state in sync.Players)
            {
                alive.Add(state.Id);
                if (!state.HasPosition) continue;
                Avatar a;
                if (!avatars.TryGetValue(state.Id, out a))
                {
                    a = new Avatar(); a.Root = CreateHumanoid(state.Nickname, ColorFor(state.Id));
                    a.Root.transform.position = new Vector3(state.X, state.Y, state.Z);
                    avatars.Add(state.Id, a);
                }
                a.State = state;
                a.Target = new Vector3(state.X, state.Y, state.Z);
                a.TargetRotation = Quaternion.Euler(0f, state.RotationY, 0f);
            }
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, Avatar> pair in avatars)
            {
                if (!alive.Contains(pair.Key)) { UnityEngine.Object.Destroy(pair.Value.Root); remove.Add(pair.Key); continue; }
                Avatar a = pair.Value;
                // Плавная коррекция позиции и поворота вместо телепортации на каждый пакет.
                float t = Mathf.Clamp01(deltaTime * 9f);
                a.Root.transform.position = Vector3.Lerp(a.Root.transform.position, a.Target, t);
                a.Root.transform.rotation = Quaternion.Slerp(a.Root.transform.rotation, a.TargetRotation, t);
            }
            for (int i = 0; i < remove.Count; i++) avatars.Remove(remove[i]);
        }
        public void DrawNames(Camera camera)
        {
            if (camera == null) return;
            foreach (Avatar a in avatars.Values)
            {
                if (a.State == null || a.Root == null) continue;
                Vector3 p = camera.WorldToScreenPoint(a.Root.transform.position + new Vector3(0f, 1.25f, 0f));
                if (p.z > 0f) GUI.Label(new Rect(p.x - 60f, Screen.height - p.y - 18f, 120f, 22f), a.State.Nickname);
            }
        }
        public void Clear()
        {
            foreach (Avatar a in avatars.Values) if (a.Root != null) UnityEngine.Object.Destroy(a.Root);
            avatars.Clear();
        }
        private static GameObject CreateHumanoid(string nickname, Color color)
        {
            GameObject root = new GameObject("MSCMP_Remote_" + nickname);
            Part(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.48f, 0.72f, 0.30f), color);
            Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.83f, 0f), new Vector3(0.34f, 0.34f, 0.34f), new Color(0.86f, 0.72f, 0.60f));
            Part(root, PrimitiveType.Cylinder, "LeftArm", new Vector3(-0.36f, 0.05f, 0f), new Vector3(0.13f, 0.43f, 0.13f), color).transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            Part(root, PrimitiveType.Cylinder, "RightArm", new Vector3(0.36f, 0.05f, 0f), new Vector3(0.13f, 0.43f, 0.13f), color).transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Part(root, PrimitiveType.Cylinder, "LeftLeg", new Vector3(-0.15f, -0.65f, 0f), new Vector3(0.15f, 0.42f, 0.15f), new Color(0.18f, 0.20f, 0.25f));
            Part(root, PrimitiveType.Cylinder, "RightLeg", new Vector3(0.15f, -0.65f, 0f), new Vector3(0.15f, 0.42f, 0.15f), new Color(0.18f, 0.20f, 0.25f));
            return root;
        }
        private static GameObject Part(GameObject parent, PrimitiveType type, string partName, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type); part.name = partName; part.transform.parent = parent.transform;
            part.transform.localPosition = pos; part.transform.localScale = scale;
            Collider c = part.GetComponent<Collider>(); if (c != null) UnityEngine.Object.Destroy(c);
            Renderer r = part.GetComponent<Renderer>(); if (r != null) r.material.color = color;
            return part;
        }
        private static Color ColorFor(string id)
        {
            int h = id == null ? 0 : id.GetHashCode();
            float r = 0.35f + ((h & 255) / 255f) * 0.55f;
            float g = 0.35f + (((h >> 8) & 255) / 255f) * 0.55f;
            float b = 0.35f + (((h >> 16) & 255) / 255f) * 0.55f;
            return new Color(r, g, b);
        }
    }
}
