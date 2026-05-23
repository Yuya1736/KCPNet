using KCPExampleProtocol;
using KCPNet;
using System;

/// <summary>
/// .net 控制台客户端session
/// </summary>
namespace KCPExampleClient
{
    class ClientSession : KCPSession<NetMessage>
    {
        protected override void OnConnected()
        {

        }

        protected override void OnDisconnected()
        {

        }

        protected override void OnReceiveMessage(NetMessage msg)
        {
            KCPTool.ColorLog(KCPTool.LogColor.Magenta, "sid:{0}, receiveByServer:{1}", sId, msg.info);
        }

        protected override void OnUpdate(DateTime now)
        {

        }
    }
}
