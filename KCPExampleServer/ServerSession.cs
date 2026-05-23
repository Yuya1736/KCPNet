using KCPExampleProtocol;
using KCPNet;
using System;

namespace KCPExampleServer
{
    class ServerSession : KCPSession<NetMessage>
    {
        protected override void OnConnected()
        {
            checkCounter = 0;
            checkTime = DateTime.Now.AddSeconds(5);
            KCPTool.ColorLog(KCPTool.LogColor.Green, "client online,sid:{0}", sId);
        }

        protected override void OnDisconnected()
        {
            KCPTool.Warning("client offline,sid:{0}", sId);
        }

        protected override void OnReceiveMessage(NetMessage msg)
        {
            KCPTool.ColorLog(KCPTool.LogColor.Magenta, "sid:{0},receive By Client,cmd:{1},info:{2}", sId, msg.cmd.ToString(), msg.info);

            if (msg.cmd == CMD.NetPing)
            {
                if (msg.netPing.isOver)
                {
                    CloseSession();
                }
                else
                {
                    checkCounter = 0;
                    NetMessage pingMessage = new NetMessage
                    {
                        cmd = CMD.NetPing,
                        netPing = new NetPing
                        {
                            isOver = false
                        }
                    };
                    SendMessage(pingMessage);
                }
            }
        }

        private int checkCounter;
        DateTime checkTime;

        protected override void OnUpdate(DateTime now)
        {
            if (now > checkTime)
            {
                checkTime = now.AddSeconds(5);
                checkCounter++;
                if (checkCounter > 3)
                {
                    CloseSession();
                }
            }
        }
    }
}
