using System;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // Попытка взять настоящую модель из сцены игры. Клонируется скелет с мешем,
    // все скрипты (в т.ч. PlayMaker FSM), коллайдеры, физика, звук, камеры и свет вырезаются,
    // поэтому логика игры на копии не запускается. Нет пригодных анимаций — модель не используется.
    public sealed class GameModelAvatar : IAvatarModel
    {
        private GameObject root;
        private Animation animation;
        private Transform headBone;
        private string idleClip, walkClip, runClip, jumpClip, fallClip, current;

        public GameObject Root { get { return root; } }
        public Transform HeadAnchor { get { return headBone; } }

        private GameModelAvatar() { }

        public static GameModelAvatar TryCreate(string id, Transform localPlayer)
        {
            SkinnedMeshRenderer source = FindSource(localPlayer);
            if (source == null) return null;
            Transform cloneRoot = SkeletonRoot(source);
            if (cloneRoot == null) return null;

            GameObject copy = UnityEngine.Object.Instantiate(cloneRoot.gameObject);
            copy.name = "MSCMP_GameModel_" + id;
            Strip(copy);

            Animation[] anims = copy.GetComponentsInChildren<Animation>(true);
            Animation anim = anims.Length > 0 ? anims[0] : null;
            if (anim == null || CountClips(anim) == 0) { UnityEngine.Object.DestroyImmediate(copy); return null; }

            GameModelAvatar m = new GameModelAvatar();
            m.root = copy; m.animation = anim;
            m.animation.playAutomatically = false;
            m.animation.cullingType = AnimationCullingType.AlwaysAnimate;

            Animator[] animators = copy.GetComponentsInChildren<Animator>(true);
            Animator animator = animators.Length > 0 ? animators[0] : null;
            if (animator != null) { m.headBone = animator.GetBoneTransform(HumanBodyBones.Head); animator.enabled = false; }

            foreach (AnimationState st in anim)
            {
                string n = st.name.ToLowerInvariant();
                if (m.idleClip == null && (n.Contains("idle") || n.Contains("stand"))) m.idleClip = st.name;
                if (m.walkClip == null && n.Contains("walk")) m.walkClip = st.name;
                if (m.runClip == null && (n.Contains("run") || n.Contains("sprint"))) m.runClip = st.name;
                if (m.jumpClip == null && n.Contains("jump")) m.jumpClip = st.name;
                if (m.fallClip == null && (n.Contains("fall") || n.Contains("air"))) m.fallClip = st.name;
            }
            if (m.walkClip == null && m.idleClip == null) { UnityEngine.Object.DestroyImmediate(copy); return null; }
            return m;
        }

        // Диагностика для лобби: что вообще есть в сцене.
        public static string DescribeCandidates(Transform localPlayer)
        {
            SkinnedMeshRenderer[] all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>();
            if (all == null || all.Length == 0) return "no skinned meshes in scene";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int shown = 0;
            for (int i = 0; i < all.Length && shown < 8; i++)
            {
                SkinnedMeshRenderer r = all[i];
                if (r == null) continue;
                if (localPlayer != null && r.transform.IsChildOf(localPlayer)) continue;
                Animation a = r.GetComponentInParent<Animation>();
                sb.Append(r.name).Append(" h=").Append(r.bounds.size.y.ToString("0.00"))
                  .Append(" clips=").Append(a == null ? 0 : CountClips(a)).Append("; ");
                shown++;
            }
            return shown == 0 ? "no suitable skinned meshes" : sb.ToString();
        }

        private static SkinnedMeshRenderer FindSource(Transform localPlayer)
        {
            SkinnedMeshRenderer[] all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>();
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
            {
                SkinnedMeshRenderer r = all[i];
                if (r == null || r.rootBone == null) continue;
                if (localPlayer != null && r.transform.IsChildOf(localPlayer)) continue;
                float h = r.bounds.size.y;
                if (h < 1.2f || h > 2.3f) continue;
                Animation a = r.GetComponentInParent<Animation>();
                if (a == null || CountClips(a) == 0) continue;
                return r;
            }
            return null;
        }

        private static Transform SkeletonRoot(SkinnedMeshRenderer r)
        {
            Transform t = r.transform;
            for (int i = 0; i < 4 && t.parent != null; i++)
            {
                if (t.parent.childCount > 20) break;
                if (r.rootBone != null && !r.rootBone.IsChildOf(t.parent)) break;
                t = t.parent;
            }
            return t;
        }

        private static void Strip(GameObject copy)
        {
            MonoBehaviour[] s = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < s.Length; i++) if (s[i] != null) UnityEngine.Object.DestroyImmediate(s[i]);
            Collider[] c = copy.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < c.Length; i++) if (c[i] != null) UnityEngine.Object.DestroyImmediate(c[i]);
            Rigidbody[] b = copy.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < b.Length; i++) if (b[i] != null) UnityEngine.Object.DestroyImmediate(b[i]);
            AudioSource[] a = copy.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < a.Length; i++) if (a[i] != null) UnityEngine.Object.DestroyImmediate(a[i]);
            Camera[] cam = copy.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cam.Length; i++) if (cam[i] != null) UnityEngine.Object.DestroyImmediate(cam[i]);
            Light[] l = copy.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < l.Length; i++) if (l[i] != null) UnityEngine.Object.DestroyImmediate(l[i]);
        }

        private static int CountClips(Animation a)
        {
            if (a == null) return 0;
            int n = 0;
            foreach (AnimationState st in a) n++;
            return n;
        }

        public void SetColor(Color color) { }   // у игровой модели свои материалы

        public void Tick(Vector3 position, float yaw, float pitch, MoveState state, float speed, float dt)
        {
            if (root == null) return;
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (headBone != null) headBone.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -80f, 80f), 0f, 0f);

            string clip = idleClip;
            if (state == MoveState.Running && runClip != null) clip = runClip;
            else if (state == MoveState.Walking && walkClip != null) clip = walkClip;
            else if (state == MoveState.Jumping && jumpClip != null) clip = jumpClip;
            else if (state == MoveState.Falling && fallClip != null) clip = fallClip;
            else if (speed > 0.2f && walkClip != null) clip = walkClip;
            if (clip == null || animation == null || clip == current) return;
            try { animation.CrossFade(clip, 0.15f); current = clip; }
            catch (Exception) { current = null; }
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; animation = null; headBone = null; current = null;
        }
    }

    public static class AvatarFactory
    {
        public static bool UseGameModel = false;

        public static IAvatarModel Create(string id, Transform localPlayer, out string diagnostics)
        {
            diagnostics = null;
            if (UseGameModel)
            {
                try
                {
                    GameModelAvatar game = GameModelAvatar.TryCreate(id, localPlayer);
                    if (game != null) { diagnostics = "game model"; return game; }
                    diagnostics = "game model not found, procedural used";
                }
                catch (Exception ex) { diagnostics = "game model failed: " + ex.Message; }
            }
            return new HumanoidAvatar(id, HumanoidAvatar.ColorFor(id));
        }
    }
}