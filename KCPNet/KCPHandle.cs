using System;
using System.Buffers;
using System.Net.Sockets.Kcp;

namespace KCPNet
{
    public class KCPHandle : IKcpCallback
    {
        public Action<Memory<byte>> Out;
        public Action<byte[]> Recv;

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            Out?.Invoke(buffer.Memory.Slice(0, avalidLength));
        }

        public void Receive(byte[] buffer)
        {
            Recv?.Invoke(buffer);
        }
    }

    public class KCPRentable : IRentable
    {
        public static readonly KCPRentable Default = new KCPRentable();

        public IMemoryOwner<byte> RentBuffer(int length)
        {
            return MemoryPool<byte>.Shared.Rent(length);
        }
    }
}
