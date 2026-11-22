using System;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // Контракт модели машины: заглушка сегодня, клонирование настоящей — завтра.
    public interface ICarModel
    {
        GameObject Root { get; }
        void Tick(Vector3 position, float yaw, float speed);
        void SetColor(Color color);
        void Dispose();
    }

    // Временная машина из примитивов: корпус + кабина + 4 колеса. Корень в центре корпуса.
    public sealed class PlaceholderCarModel : ICarModel
    {
        private GameObject root;
        private Material material;

        public GameObject Root { get { return root; } }

        public PlaceholderCarModel(string id, Color color)
        {
            root = new GameObject("MSCMP_RemoteCar_" + id);
            material = CreateMaterial(color);
            Part("Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.5f, 3.4f));
            Part("Cabin", PrimitiveType.Cube, new Vector3(0f, 0.45f, -0.1f), new Vector3(1.3f, 0.45f, 1.6f));
            Part("WheelFL", PrimitiveType.Cylinder, new Vector3(-0.85f, -0.3f, 1.1f), new Vector3(0.3f, 0.06f, 0.3f), true);
            Part("WheelFR", PrimitiveType.Cylinder, new Vector3(0.85f, -0.3f, 1.1f), new Vector3(0.3f, 0.06f, 0.3f), true);
            Part("WheelRL", PrimitiveType.Cylinder, new Vector3(-0.85f, -0.3f, -1.1f), new Vector3(0.3f, 0.06f, 0.3f), true);
            Part("WheelRR", PrimitiveType.Cylinder, new Vector3(0.85f, -0.3f, -1.1f), new Vector3(0.3f, 0.06f, 0.3f), true);
        }

        public void SetColor(Color color) { if (material != null && material.HasProperty("_Color")) material.color = color; }

        public void Tick(Vector3 position, float yaw, float speed)
        {
            if (root == null) return;
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            if (material != null) UnityEngine.Object.Destroy(material);
            material = null;
        }

        private void Part(string name, PrimitiveType type, Vector3 localPos, Vector3 scale, bool wheel = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            Transform t = go.transform;
            t.parent = root.transform;
            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            if (wheel) t.localRotation = Quaternion.Euler(0f, 0f, 90f);   // колесо на бок
            t.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = material;
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