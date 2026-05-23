using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Threading;
using System.Threading.Tasks;

namespace KCPNet
{
    public enum KCPState
    {
        None,
        Connected,
        Disconnected
    }

    public abstract class KCPSession<T> where T : KCPMessage, new()
    {
        protected uint sId;
        protected KCPState state;
        private IPEndPoint remoteIp;
        private Action<byte[], IPEndPoint> _udpSender;

        private Kcp<KcpSegment> _kcp;
        private KCPHandle _kcpHandle;
        private readonly object _kcpLock = new object();

        public Action<uint> onSessionClosed;

        private CancellationTokenSource _cancellationTokenSource;//取消令牌源
        private CancellationToken _cancellationToken;//取消令牌

        public bool isConnected => state == KCPState.Connected;

        public void InitSession(uint sessionId, Action<byte[], IPEndPoint> udpSender, IPEndPoint remotePoint)
        {
            sId = sessionId;
            remoteIp = remotePoint;

            _udpSender = udpSender;
            _kcpHandle = new KCPHandle();
            _kcp = new SimpleSegManager.Kcp(sId, _kcpHandle, KCPRentable.Default);
            state = KCPState.Connected;

            // 极速模式
            _kcp.NoDelay(1, 10, 2, 1);
            // WndSize该调用将会设置协议的最大发送窗口和最大接收窗口大小，默认为32.
            // 这个可以理解为 TCP 的 SND_BUF 和 RCV_BUF，只不过单位不一样 SND/RCV_BUF 单位是字节，这个单位是包。
            _kcp.WndSize(64, 64);
            // 纯算法协议并不负责探测 MTU，默认 mtu是1400字节，可以使用ikcp_setmtu来设置该值。该值将会影响数据包归并及分片时候的最大传输单元。
            _kcp.SetMtu(512);

            _kcpHandle.Out = (Memory<byte> message) =>
            {
                byte[] data = message.ToArray();
                _udpSender?.Invoke(data, remoteIp);
            };

            _kcpHandle.Recv = (byte[] buffer) =>
            {
                buffer = KCPTool.DeCompress(buffer);
                T data = KCPTool.DeSerialize<T>(buffer);
                OnReceiveMessage(data);
            };

            OnConnected();

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            Task.Run(Update, _cancellationToken);
        }

        public void ReceiveData(byte[] data)
        {
            lock (_kcpLock)
            {
                _kcp?.Input(data);
            }
        }

        async void Update()
        {
            try
            {
                while (true)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    DateTime now = DateTime.Now;
                    OnUpdate(now);
                    lock (_kcpLock)
                    {
                        if (_kcp == null) break;
                        _kcp.Update(now);
                        int len;
                        while ((len = _kcp.PeekSize()) > 0)
                        {
                            byte[] buffer = new byte[len];
                            if (_kcp.Recv(buffer) > 0)
                            {
                                _kcpHandle.Receive(buffer);
                            }
                        }
                    }
                    await Task.Delay(10);
                }
            }
            catch (Exception e)
            {
                KCPTool.Warning("Session {0} Update Error: {1}", sId, e.Message);
            }
        }

        public void SendMessage(T msg)
        {
            if (isConnected)
            {
                byte[] bytes = KCPTool.Serialize(msg);
                if (bytes != null)
                {
                    SendMessage(bytes);
                }
            }
            else
            {
                KCPTool.Warning("没有连接，不能发送消息");
            }
        }

        public void SendMessage(byte[] msgBytes)
        {
            if (isConnected)
            {
                msgBytes = KCPTool.Compress(msgBytes);
                lock (_kcpLock)
                {
                    if (_kcp == null)
                    {
                        KCPTool.Warning("Session {0} kcp instance is null, cannot send", sId);
                        return;
                    }
                    int result = _kcp.Send(msgBytes.AsSpan());
                    if (result < 0)
                    {
                        KCPTool.Warning("Session {0} Send returned {1} (data length: {2})", sId, result, msgBytes.Length);
                    }
                }
            }
            else
            {
                KCPTool.Warning("没有连接，不能发送消息");
            }
        }

        public void CloseSession()
        {
            _cancellationTokenSource.Cancel();
            OnDisconnected();

            onSessionClosed?.Invoke(sId);
            onSessionClosed = null;

            sId = 0;
            state = KCPState.Disconnected;
            remoteIp = null;
            _udpSender = null;
            lock (_kcpLock)
            {
                _kcp = null;
            }
            _kcpHandle = null;
            _cancellationTokenSource = null;
        }

        public override bool Equals(object obj)
        {
            if (obj is KCPSession<T>)
            {
                KCPSession<T> us = obj as KCPSession<T>;
                return sId == us.sId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return sId.GetHashCode();
        }

        public uint GetSessionID()
        {
            return sId;
        }


        protected abstract void OnReceiveMessage(T data);
        protected abstract void OnUpdate(DateTime time);
        protected abstract void OnConnected();
        protected abstract void OnDisconnected();
    }
}
