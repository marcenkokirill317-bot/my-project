using System;
using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    public class MSCMPMod : Mod
    {
        public override string ID { get { return "MSC_Multiplayer"; } }
        public override string Name { get { return "MSC Multiplayer"; } }
        public override string Author { get { return "MSC Multiplayer Team"; } }
        public override string Version { get { return "0.1.9-dev"; } }
        public override string Description { get { return "Experimental lobby, remote avatars and movement synchronization."; } }

        private NetworkClient network;
        private PlayerSyncManager sync;
        private RemoteAvatarManager avatars;
        private bool windowOpen;
        private Rect windowRect = new Rect(20f, 20f, 330f, 330f);
        private string host = "127.0.0.1", portText = "27020", nickname = "Player";
        private float sendTimer, pingTimer;
        private bool localPlayerFound;
        private bool wasConnected;
        private bool wasConnecting;
        private string status = "Disconnected";
        private const float SendInterval = 0.075f; // ~13.3 updates/sec
        private const float PingInterval = 2f;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnLoaded);
            SetupFunction(Setup.Update, OnUpdate);
            SetupFunction(Setup.OnGUI, OnGui);
        }
        private void OnLoaded()
        {
            network = new NetworkClient(); sync = new PlayerSyncManager(); avatars = new RemoteAvatarManager();
            host = PlayerPrefs.GetString("MSCMP_Host", "127.0.0.1");
            portText = PlayerPrefs.GetString("MSCMP_Port", "27020");
            nickname = PlayerPrefs.GetString("MSCMP_Nick", "Player");
            ModConsole.Print("[MSC Multiplayer] 0.1.9-dev loaded. Press F8 for lobby.");
        }
        private void OnUpdate()
        {
            if (network == null) return;
            if (Input.GetKeyDown(KeyCode.F8)) windowOpen = !windowOpen;
            network.CheckConnectionTimeout();
            // Also clear partial roster data if WELCOME was lost or the server rejected
            // the connection after sending preliminary lobby packets.
            if ((wasConnected || wasConnecting) && !network.IsConnected && !network.IsConnecting)
            {
                sync.Clear();
                avatars.Clear();
                localPlayerFound = false;
            }
            wasConnected = network.IsConnected;
            wasConnecting = network.IsConnecting;
            string packet;
            while (network.TryDequeue(out packet)) sync.Handle(packet);
            if (network.IsConnected)
            {
                sendTimer += Time.deltaTime; pingTimer += Time.deltaTime;
                if (sendTimer >= SendInterval)
                {
                    sendTimer = 0f;
                    Transform player = FindLocalPlayer();
                    localPlayerFound = player != null;
                    if (player != null)
                    {
                        Vector3 p = player.position;
                        network.SendPosition(p.x, p.y, p.z, player.eulerAngles.y);
                    }
                }
                if (pingTimer >= PingInterval) { pingTimer = 0f; network.Ping(); }
                status = localPlayerFound ? "Connected (" + sync.Count + " remote)" : "Connected; player object not found";
            }
            else if (!String.IsNullOrEmpty(network.LastError)) status = "Error: " + network.LastError;
            else if (!network.IsConnecting) status = "Disconnected";
            avatars.Update(sync, Time.deltaTime);
        }
        private Transform FindLocalPlayer()
        {
            // Названия объектов отличаются между версиями/сценами игры. Не подменяем игрока камерой,
            // если объект персонажа не найден: это предотвращает отправку неверных координат.
            GameObject go = GameObject.Find("PLAYER");
            if (go == null) go = GameObject.Find("Player");
            return go == null ? null : go.transform;
        }
        private void OnGui()
        {
            if (avatars != null) avatars.DrawNames(Camera.main);
            if (windowOpen) windowRect = GUI.Window(87017, windowRect, DrawLobby, "MSC Multiplayer - Lobby");
        }
        private void DrawLobby(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Server IP / Host"); host = GUILayout.TextField(host);
            GUILayout.Label("UDP Port"); portText = GUILayout.TextField(portText);
            GUILayout.Label("Nickname"); nickname = GUILayout.TextField(nickname);
            GUILayout.Space(6f);
            if (network != null && network.IsConnected)
            {
                GUILayout.Label("Status: " + status);
                GUILayout.Label("Your ID: " + (network.PlayerId ?? "..."));
                if (GUILayout.Button("Disconnect")) Disconnect();
            }
            else
            {
                GUILayout.Label("Status: " + status);
                GUI.enabled = network == null || !network.IsConnecting;
                if (GUILayout.Button("Connect")) Connect();
                GUI.enabled = true;
                if (network != null && network.IsConnecting && GUILayout.Button("Cancel")) Disconnect();
            }
            GUILayout.Space(6f); GUILayout.Label("Players online: " + (sync == null ? 0 : sync.Count + (network != null && network.IsConnected ? 1 : 0)) + "/4");
            if (network != null && network.IsConnected) GUILayout.Label(" - " + nickname + " [you]");
            if (sync != null) foreach (RemotePlayerState p in sync.Players) GUILayout.Label(" - " + p.Nickname + " [" + p.Id + "]");
            GUILayout.Space(6f);
            GUILayout.Label("F8 - show/hide lobby");
            GUILayout.EndVertical(); GUI.DragWindow();
        }
        private void Connect()
        {
            int port;
            if (String.IsNullOrEmpty(host) || !Int32.TryParse(portText, out port) || port < 1 || port > 65535)
            { status = "Enter a valid host and port"; return; }
            if (String.IsNullOrEmpty(nickname) || String.IsNullOrEmpty(nickname.Trim()))
            { status = "Enter a nickname"; return; }
            nickname = nickname.Trim();
            if (nickname.Length > 20) nickname = nickname.Substring(0, 20);
            PlayerPrefs.SetString("MSCMP_Host", host.Trim());
            PlayerPrefs.SetString("MSCMP_Port", port.ToString());
            PlayerPrefs.SetString("MSCMP_Nick", nickname);
            PlayerPrefs.Save();
            sync.Clear(); status = "Connecting...";
            if (!network.Connect(host.Trim(), port, nickname.Trim())) status = "Error: " + network.LastError;
        }
        private void Disconnect()
        {
            if (network != null) network.Disconnect();
            wasConnected = false;
            wasConnecting = false;
            localPlayerFound = false;
            if (sync != null) sync.Clear();
            if (avatars != null) avatars.Clear();
            status = "Disconnected";
        }
    }
}
