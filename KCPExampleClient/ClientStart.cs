using KCPExampleProtocol;
using KCPNet;
using System;
using System.Threading.Tasks;
using static KCPNet.KCPTool;

namespace KCPExampleClient
{
    public class ClientStart
    {
        static KCPNet<ClientSession, NetMessage> client;
        static Task<bool> checkTask = null;

        static void Main(string[] args)
        {
            string ip = "127.0.0.1";
            client = new KCPNet<ClientSession, NetMessage>();
            client.StartAsClient(ip, 17666);
            checkTask = client.ConnectServer(200, 5000);
            Task.Run(ConnectCheck);
            while (true)
            {
                string ipt = Console.ReadLine();
                if (ipt == null || ipt == "quit")
                {
                    client.CloseClient();
                    break;
                }
                else
                {
                    if (client.clientSession != null && client.clientSession.isConnected)
                    {
                        client.clientSession.SendMessage(new NetMessage { info = ipt });
                    }
                    else
                    {
                        KCPTool.Warning("连接尚未建立，无法发送消息");
                    }
                }
            }
            if (!Console.IsInputRedirected) Console.ReadKey();
        }

        private static int counter = 0;
        static async void ConnectCheck()
        {
            while (true)
            {
                await Task.Delay(3000);
                if (checkTask != null && checkTask.IsCompleted)
                {
                    if (checkTask.Result)
                    {
                        KCPTool.ColorLog(LogColor.Green, "连接服务器成功.");
                        checkTask = null;
                        await Task.Run(SendPingMessage);
                    }
                    else
                    {
                        ++counter;
                        if (counter > 4)
                        {
                            KCPTool.Error("客户端连接服务器失败{0}次，请检查网络。", counter);
                            checkTask = null;
                            break;
                        }
                        else
                        {
                            KCPTool.Warning("客户端连接服务器失败{0}次，正尝试连接中。。。", counter);
                            checkTask = client.ConnectServer(200, 5000);
                        }
                    }
                }
            }
        }

        static async void SendPingMessage()
        {
            while (true)
            {
                await Task.Delay(5000);
                if (client != null && client.clientSession != null)
                {
                    client.clientSession.SendMessage((new NetMessage
                    {
                        cmd = CMD.NetPing,
                        netPing = new NetPing
                        {
                            isOver = false
                        }
                    }));
                    KCPTool.ColorLog(LogColor.Green, "客户端发送心跳包");
                }
                else
                {
                    KCPTool.ColorLog(LogColor.Green, "心跳包任务取消");
                    break;
                }
            }
        }
    }
}

