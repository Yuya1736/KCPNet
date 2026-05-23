using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using static KCPNet.KCPTool;

namespace KCPNet
{
    [MessagePackObject]
    public abstract class KCPMessage { }

    public class KCPNet<T, K> where T : KCPSession<K>, new() where K : KCPMessage, new()
    {
        private UdpClient _udp;
        private IPEndPoint _listenEndpoint;

        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        public KCPNet()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        private void InitIOControl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            }
        }

        #region Server

        private Dictionary<uint, T> _sessionDic = null;
        public void StartAsServer(string ip, int port)
        {
            _sessionDic = new Dictionary<uint, T>();
            _listenEndpoint = new IPEndPoint(IPAddress.Parse(ip), port);
            try
            {
                _udp = new UdpClient(_listenEndpoint);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Error("端口 {0} 已被占用，请检查是否有其他服务端实例正在运行。", port);
                throw;
            }
            InitIOControl();
            ColorLog(LogColor.Green, "Server Start ...");
            Task.Run(ServerReceive, _cancellationToken);
        }

        async Task ServerReceive()
        {
            UdpReceiveResult result;
            while (true)
            {
                try
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        ColorLog(LogColor.Cyan, "服务器接收数据的任务已经取消");
                        break;
                    }

                    result = await _udp.ReceiveAsync();
                    uint sessionId = BitConverter.ToUInt32(result.Buffer, 0);
                    if (sessionId == 0)
                    {
                        sessionId = GenerateUniqueSessionID();
                        byte[] sidBytes = BitConverter.GetBytes(sessionId);
                        byte[] convBytes = new byte[8];
                        Array.Copy(sidBytes, 0, convBytes, 4, 4);
                        SendUdpMessage(convBytes, result.RemoteEndPoint);
                    }
                    else
                    {
                        T session;
                        lock (_sessionDic)
                        {
                            if (!_sessionDic.TryGetValue(sessionId, out session))
                            {
                                session = new T();
                                session.InitSession(sessionId, SendUdpMessage, result.RemoteEndPoint);
                                session.onSessionClosed = OnServerSessionClose;
                                _sessionDic.Add(sessionId, session);
                            }
                        }
                        session.ReceiveData(result.Buffer);
                    }
                }
                catch (Exception e)
                {
                    Warning("服务器udp接收数据异常:{0}", e.ToString());
                }
            }
        }

        void OnServerSessionClose(uint sid)
        {
            if (_sessionDic.ContainsKey(sid))
            {
                _sessionDic.Remove(sid);
                Warning("session:{0} remove in sessionDic.", sid);
            }
            else
            {
                Error("session:{0} cannot find in sessionDic.", sid);
            }
        }

        public void CloseServer()
        {
            foreach (var session in _sessionDic)
            {
                session.Value.CloseSession();
            }
            _sessionDic = null;
            if (_udp != null)
            {
                _udp.Close();
                _udp = null;
                _cancellationTokenSource.Cancel();
            }
        }

        #endregion

        #region Client
        public T clientSession;
        private IPEndPoint _remoteIp;
        public void StartAsClient(string ip, int port)
        {
            _udp = new UdpClient(0);
            _remoteIp = new IPEndPoint(IPAddress.Parse(ip), port);

            Task.Run(ClientReceive, _cancellationToken);
        }
        public Task<bool> ConnectServer(int interval, int maxIntervalSum = 5000)
        {
            SendUdpMessage(new byte[4], _remoteIp);
            int checkTimes = 0;
            Task<bool> task = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);
                    checkTimes += interval;
                    if (clientSession != null && clientSession.isConnected)
                    {
                        return true;
                    }
                    else
                    {
                        if (checkTimes > maxIntervalSum)
                        {
                            return false;
                        }
                    }
                }
            });
            return task;
        }
        async Task ClientReceive()
        {
            UdpReceiveResult result;
            while (true)
            {
                try
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    result = await _udp.ReceiveAsync();
                    if (Equals(_remoteIp, result.RemoteEndPoint))
                    {
                        uint sId = BitConverter.ToUInt32(result.Buffer, 0);
                        if (sId == 0)
                        {
                            if (clientSession != null && clientSession.isConnected)
                            {
                                ColorLog(LogColor.Yellow, "sId has exist, ignore");
                            }
                            else
                            {
                                sId = BitConverter.ToUInt32(result.Buffer, 4);
                                clientSession = new T();
                                clientSession.InitSession(sId, SendUdpMessage, _remoteIp);
                                clientSession.onSessionClosed = OnSessionClosed;
                            }
                        }
                        else
                        {
                            if (clientSession != null && clientSession.isConnected)
                            {
                                clientSession.ReceiveData(result.Buffer);
                            }
                            else
                            {
                                Warning("客户端正在初始化中...");
                            }
                        }
                    }
                    else
                    {
                        Warning("客户端udp接收了非法目标数据.");
                    }

                }
                catch (Exception e)
                {
                    ColorLog(LogColor.Yellow, "ClientReceive Exception {0}", e.ToString());
                }
            }
        }
        public void OnSessionClosed(uint sId)
        {
            _cancellationTokenSource.Cancel();
            if (_udp != null)
            {
                _udp.Close();
                _udp = null;
            }
            ColorLog(LogColor.Green, "Client Session Closed，sId : {0}", sId);
        }
        public void CloseClient()
        {
            clientSession?.CloseSession();
            clientSession = null;
            _remoteIp = null;
        }
        #endregion


        public async void SendUdpMessage(byte[] message, IPEndPoint remoteIp)
        {
            try
            {
                if (_udp != null)
                {
                    await _udp.SendAsync(message, message.Length, remoteIp);
                }
            }
            catch (Exception e)
            {
                Warning("UDP发送异常:{0}", e.Message);
            }
        }

        public void BroadcastMessage(K msg)
        {
            byte[] bytes = KCPTool.Serialize(msg);
            foreach (var session in _sessionDic)
            {
                if (session.Value != null)
                    session.Value.SendMessage(bytes);
            }
        }

        private uint sid = 0;
        public uint GenerateUniqueSessionID()
        {
            lock (_sessionDic)
            {
                while (true)
                {
                    ++sid;
                    if (sid == uint.MaxValue)
                    {
                        sid = 1;
                    }
                    if (!_sessionDic.ContainsKey(sid))
                    {
                        break;
                    }
                }
            }
            return sid;
        }
    }
}
