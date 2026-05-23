using KCPNet;
using MessagePack;

namespace KCPExampleProtocol
{
    [MessagePackObject]
    public class NetMessage : KCPMessage
    {
        [Key(0)] public CMD cmd;
        [Key(1)] public NetPing netPing;
        [Key(2)] public string info;
    }

    [MessagePackObject]
    public class NetPing
    {
        [Key(0)] public bool isOver;
    }

    public enum CMD
    {
        None,
        NetPing,
    }
}
