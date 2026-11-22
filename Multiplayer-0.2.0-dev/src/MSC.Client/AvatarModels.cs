using System;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    public interface IAvatarModel
    {
        GameObject Root { get; }
        Transform HeadAnchor { get; }
        void SetColor(Color color);
        void Tick(Vector3 position, float yaw, float pitch, MoveState state, float speed, float deltaTime);
        void Dispose();
    }

    // Человеческие пропорции: ноги ~0.94, торс+шея ~0.66, голова ~0.24 => рост ~1.84 м.
    // Корень (hips) ставится в присылаемую точку; ступни оказываются на земле.
    public sealed class HumanoidAvatar : IAvatarModel
    {
        public static float ModelScale = 1.0f;   // подгонка роста под персонажа игры

        private static readonly Color[] Palette =
        {
            new Color(0.90f,0.35f,0.30f), new Color(0.30f,0.65f,0.90f),
            new Color(0.40f,0.80f,0.45f), new Color(0.95f,0.75f,0.30f),
            new Color(0.70f,0.45f,0.90f), new Color(0.95f,0.55f,0.25f),
            new Color(0.35f,0.85f,0.80f), new Color(0.85f,0.50f,0.70f)
        };
        private GameObject root;
        private Transform hips, spine, head, armL, armR, legL, legR;
        private Material material;
        private float phase, blend, idleTime;

        public GameObject Root { get { return root; } }
        public Transform HeadAnchor { get { return head; } }

        public HumanoidAvatar(string id, Color color)
        {
            root = new GameObject("MSCMP_Remote_" + id);
            material = CreateMaterial(color);

            hips = Bone("Hips", root.transform, Vector3.zero);
            spine = Bone("Spine", hips, Vector3.zero);

            // Торс: скруглённый, уже плеч, чем у куба.
            Part("Torso", PrimitiveType.Capsule, spine, new Vector3(0f, 0.30f, 0f), new Vector3(0.34f, 0.30f, 0.20f));
            Part("Neck", PrimitiveType.Cylinder, spine, new Vector3(0f, 0.62f, 0f), new Vector3(0.09f, 0.06f, 0.09f));

            // Голова: кость + меш, чтобы наклон головы работал и ник крепился к кости.
            head = Bone("Head", spine, new Vector3(0f, 0.66f, 0f));
            Part("HeadMesh", PrimitiveType.Sphere, head, new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.26f, 0.24f));

            // Руки: плечо -> предплечье -> кисть, всё дети кости плеча (качаются вместе).
            armL = Bone("ArmL", spine, new Vector3(-0.18f, 0.52f, 0f));
            armR = Bone("ArmR", spine, new Vector3(0.18f, 0.52f, 0f));
            Arm(armL); Arm(armR);
            armL.localRotation = Quaternion.Euler(0f, 0f, 8f);
            armR.localRotation = Quaternion.Euler(0f, 0f, -8f);

            // Ноги: бедро -> голень одной капсулой + ступня.
            legL = Bone("LegL", hips, new Vector3(-0.10f, -0.02f, 0f));
            legR = Bone("LegR", hips, new Vector3(0.10f, -0.02f, 0f));
            Leg(legL); Leg(legR);
        }

        private void Arm(Transform shoulder)
        {
            Part("UpperArm", PrimitiveType.Capsule, shoulder, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.16f, 0.09f));
            Part("Forearm", PrimitiveType.Capsule, shoulder, new Vector3(0f, -0.42f, 0f), new Vector3(0.08f, 0.14f, 0.08f));
            Part("Hand", PrimitiveType.Sphere, shoulder, new Vector3(0f, -0.58f, 0f), new Vector3(0.09f, 0.10f, 0.09f));
        }

        private void Leg(Transform hip)
        {
            Part("LegMesh", PrimitiveType.Capsule, hip, new Vector3(0f, -0.46f, 0f), new Vector3(0.13f, 0.46f, 0.13f));
            Part("Foot", PrimitiveType.Cube, hip, new Vector3(0f, -0.90f, 0.06f), new Vector3(0.12f, 0.08f, 0.26f));
        }

        public static Color ColorFor(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (id != null) for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
                return Palette[(int)(h % (uint)Palette.Length)];
            }
        }

        public void SetColor(Color color) { if (material != null && material.HasProperty("_Color")) material.color = color; }

        public void Tick(Vector3 position, float yaw, float pitch, MoveState state, float speed, float dt)
        {
            if (root == null) return;
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            root.transform.localScale = Vector3.one * ModelScale;
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -80f, 80f), 0f, 0f);

            float sp = Mathf.Clamp(speed, 0f, 8f);
            bool moving = state == MoveState.Walking || state == MoveState.Running || sp > 0.2f;
            if (moving) { phase += dt * Mathf.Lerp(4.5f, 9.5f, Mathf.Clamp01(sp / 5f)); idleTime = 0f; } else idleTime += dt;
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, dt * 6f);

            float swing = Mathf.Sin(phase) * Mathf.Lerp(20f, 48f, Mathf.Clamp01(sp / 5f)) * blend;
            legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 8f);
            armR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, -8f);

            hips.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.035f * blend + Mathf.Sin(idleTime * 1.6f) * 0.012f * (1f - blend), 0f);
            spine.localRotation = Quaternion.Euler(sp * 2.2f * blend, 0f, 0f);

            if (state == MoveState.Jumping)
            {
                legL.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                legR.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                armL.localRotation = Quaternion.Euler(-120f, 0f, 8f);
                armR.localRotation = Quaternion.Euler(-120f, 0f, -8f);
            }
            else if (state == MoveState.Falling)
            {
                legL.localRotation = Quaternion.Euler(-15f, 0f, -12f);
                legR.localRotation = Quaternion.Euler(-15f, 0f, 12f);
                armL.localRotation = Quaternion.Euler(-70f, 0f, 30f);
                armR.localRotation = Quaternion.Euler(-70f, 0f, -30f);
            }
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            if (material != null) UnityEngine.Object.Destroy(material);
            material = null;
        }

        private static Transform Bone(string name, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            Transform t = go.transform;
            t.parent = parent; t.localPosition = localPos;
            t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            return t;
        }

        private Transform Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            Transform t = go.transform;
            t.parent = parent; t.localPosition = localPos;
            t.localRotation = Quaternion.identity; t.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = material;
            return t;
        }

        private static Material CreateMaterial(Color color)
        {
            string[] names = { "Standard", "Legacy Shaders/Diffuse", "Diffuse", "Sprites/Default" };
            Shader shader = null;
            for (int i = 0; i < names.Length && shader == null; i++) shader = Shader.Find(names[i]);
            if (shader == null) throw new InvalidOperationException("MSC Multiplayer: shader not found");
            Material m = new Material(shader);
            if (m.HasProperty("_Color")) m.color = color;
            return m;
        }
    }
}