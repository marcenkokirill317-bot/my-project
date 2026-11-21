using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MSC.Multiplayer.Client
{
    public sealed class NetworkClient : IDisposable
    {
        private readonly object connectionLock = new object();
        private readonly Queue<string> queue = new Queue<string>();
        private readonly object queueLock = new object();
        private UdpClient udp;
        private Thread thread;
        private int generation;
        private volatile bool running;
        private volatile bool connected;
        private volatile bool connecting;
        private long connectStartedTicks;
        private long lastReceiveTicks;

        public string PlayerId { get; private set; }
        public bool IsConnected { get { return connected; } }
        public bool IsConnecting { get { return connecting; } }
        public string LastError { get; private set; }

        public bool Connect(string host, int port, string nickname)
        {
            Disconnect();
            LastError = null;
            if (String.IsNullOrEmpty(host) || port < 1 || port > 65535)
            {
                LastError = "Enter a valid host and UDP port (1-65535).";
                return false;
            }

            UdpClient newUdp = null;
            int myGeneration;
            try
            {
                IPAddress ip;
                if (!IPAddress.TryParse(host, out ip))
                {
                    IPAddress[] all = Dns.GetHostAddresses(host);
                    ip = null;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].AddressFamily == AddressFamily.InterNetwork)
                        {
                            ip = all[i];
                            break;
                        }
                    }
                    if (ip == null)
                    {
                        LastError = "Host not found (IPv4)";
                        return false;
                    }
                }
                if (ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    LastError = "Only IPv4 servers are supported in this version";
                    return false;
                }

                newUdp = new UdpClient(AddressFamily.InterNetwork);
                newUdp.Connect(new IPEndPoint(ip, port));

                lock (connectionLock)
                {
                    generation++;
                    myGeneration = generation;
                    udp = newUdp;
                    running = true;
                    connected = false;
                    connecting = true;
                    PlayerId = null;
                    connectStartedTicks = DateTime.UtcNow.Ticks;
                    lastReceiveTicks = connectStartedTicks;
                    thread = new Thread(delegate() { ReceiveLoop(newUdp, myGeneration); });
                    thread.IsBackground = true;
                    thread.Name = "MSCMP UDP " + myGeneration;
                    thread.Start();
                }

                SendRaw("MSC1|HELLO|" + Clean(nickname));
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                if (newUdp != null) try { newUdp.Close(); } catch { }
                StopTransport();
                return false;
            }
        }

        public void CheckConnectionTimeout()
        {
            long now = DateTime.UtcNow.Ticks;
            if (connecting && ElapsedSeconds(now, Interlocked.Read(ref connectStartedTicks)) > 8.0)
            {
                LastError = "Connection timed out. Check IP, port and firewall.";
                StopTransport();
            }
            else if (connected && ElapsedSeconds(now, Interlocked.Read(ref lastReceiveTicks)) > 10.0)
            {
                LastError = "Server connection lost (heartbeat timeout)";
                StopTransport();
            }
        }

        public void SendPosition(float x, float y, float z, float rot)
        {
            if (IsConnected)
                SendRaw("MSC1|POS|" + F(x) + "|" + F(y) + "|" + F(z) + "|" + F(rot));
        }

        public void Ping()
        {
            if (IsConnected) SendRaw("MSC1|PING");
        }

        public bool TryDequeue(out string packet)
        {
            lock (queueLock)
            {
                if (queue.Count > 0)
                {
                    packet = queue.Dequeue();
                    return true;
                }
            }
            packet = null;
            return false;
        }

        private void ReceiveLoop(UdpClient socket, int myGeneration)
        {
            while (IsCurrent(socket, myGeneration))
            {
                try
                {
                    IPEndPoint ep = null;
                    byte[] data = socket.Receive(ref ep);
                    string packet = Encoding.UTF8.GetString(data);

                    lock (connectionLock)
                    {
                        if (generation != myGeneration || !Object.ReferenceEquals(udp, socket))
                            break;

                        Interlocked.Exchange(ref lastReceiveTicks, DateTime.UtcNow.Ticks);
                        if (packet.StartsWith("MSC1|WELCOME|", StringComparison.Ordinal))
                        {
                            string[] parts = packet.Split('|');
                            if (parts.Length >= 4 && !String.IsNullOrEmpty(parts[2]))
                            {
                                PlayerId = parts[2];
                                connected = true;
                                connecting = false;
                                LastError = null;
                            }
                        }
                        else if (packet.StartsWith("MSC1|ERROR|", StringComparison.Ordinal))
                        {
                            string[] parts = packet.Split('|');
                            LastError = parts.Length > 2
                                ? parts[2].Replace('_', ' ')
                                : "Server rejected connection";
                            connected = false;
                            connecting = false;
                            running = false;
                            PlayerId = null;
                            udp = null;
                            thread = null;
                        }
                    }

                    lock (queueLock)
                    {
                        if (queue.Count < 512) queue.Enqueue(packet);
                    }

                    if (!IsCurrent(socket, myGeneration))
                        break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    if (IsCurrent(socket, myGeneration))
                    {
                        lock (connectionLock)
                        {
                            if (generation == myGeneration && Object.ReferenceEquals(udp, socket))
                            {
                                LastError = ex.Message;
                                running = false;
                                connected = false;
                                connecting = false;
                                udp = null;
                                thread = null;
                                PlayerId = null;
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    if (IsCurrent(socket, myGeneration)) LastError = ex.Message;
                    break;
                }
            }
            try { socket.Close(); } catch { }
        }

        private bool IsCurrent(UdpClient socket, int myGeneration)
        {
            lock (connectionLock)
                return running && generation == myGeneration && Object.ReferenceEquals(udp, socket);
        }

        private void SendRaw(string packet)
        {
            UdpClient socket;
            int currentGeneration;
            lock (connectionLock)
            {
                if (!running || udp == null) return;
                socket = udp;
                currentGeneration = generation;
            }
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(packet);
                socket.Send(bytes, bytes.Length);
            }
            catch (Exception ex)
            {
                if (IsCurrent(socket, currentGeneration)) LastError = ex.Message;
            }
        }

        public void Disconnect()
        {
            UdpClient socket;
            lock (connectionLock)
            {
                socket = udp;
                if (running && socket != null)
                {
                    try
                    {
                        byte[] bye = Encoding.UTF8.GetBytes("MSC1|BYE");
                        socket.Send(bye, bye.Length);
                    }
                    catch { }
                }
            }
            StopTransport();
        }

        private void StopTransport()
        {
            UdpClient oldSocket;
            Thread oldThread;
            lock (connectionLock)
            {
                oldSocket = udp;
                oldThread = thread;
                generation++;
                running = false;
                connecting = false;
                connected = false;
                PlayerId = null;
                udp = null;
                thread = null;
            }
            try { if (oldSocket != null) oldSocket.Close(); } catch { }
            if (oldThread != null && oldThread != Thread.CurrentThread)
            {
                try { oldThread.Join(250); } catch { }
            }
            lock (queueLock) queue.Clear();
        }

        private static double ElapsedSeconds(long nowTicks, long thenTicks)
        {
            if (thenTicks <= 0) return 0.0;
            return (nowTicks - thenTicks) / (double)TimeSpan.TicksPerSecond;
        }

        private static string F(float value)
        {
            if (Single.IsNaN(value) || Single.IsInfinity(value)) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Clean(string value)
        {
            if (String.IsNullOrEmpty(value)) return "Player";
            value = value.Replace("|", "_").Replace("\r", "").Replace("\n", "").Trim();
            if (value.Length > 20) value = value.Substring(0, 20);
            return String.IsNullOrEmpty(value) ? "Player" : value;
        }

        public void Dispose() { Disconnect(); }
    }
}
