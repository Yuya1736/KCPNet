using KCPExampleProtocol;
using System;
using KCPNet;

/// <summary>
/// .net core控制台服务端
/// </summary>
namespace KCPExampleServer
{
    class ServerStart
    {
        static void Main(string[] args)
        {
            string ip = "127.0.0.1";
            KCPNet<ServerSession, NetMessage> server = new KCPNet<ServerSession, NetMessage>();
            server.StartAsServer(ip, 17666);

            while (true)
            {
                string ipt = Console.ReadLine();
                if (ipt == null)
                {
                    System.Threading.Thread.Sleep(100);
                    continue;
                }
                if (ipt == "quit")
                {
                    server.CloseServer();
                    break;
                }
                else
                {
                    server.BroadcastMessage(new NetMessage { info = ipt });
                }
            }
            if (!Console.IsInputRedirected) Console.ReadKey();
        }
    }
}
