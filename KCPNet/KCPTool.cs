using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using MessagePack;

namespace KCPNet
{
    public static class KCPTool
    {
        public static byte[] Serialize<T>(T msg) where T : KCPMessage
        {
            try
            {
                return MessagePackSerializer.Serialize(msg);
            }
            catch (MessagePackSerializationException e)
            {
                Error("序列化失败：{0}", e.Message);
                throw;
            }
        }

        public static T DeSerialize<T>(byte[] bytes) where T : KCPMessage
        {
            try
            {
                return MessagePackSerializer.Deserialize<T>(bytes);
            }
            catch (MessagePackSerializationException e)
            {
                Error("反序列化失败：{0}    字节长度：{1}", e.Message, bytes.Length);
                throw;
            }
        }

        public static byte[] Compress(byte[] input)
        {
            using (MemoryStream outMs = new MemoryStream())
            {
                using (GZipStream gzs = new GZipStream(outMs, CompressionMode.Compress, true))
                {
                    gzs.Write(input, 0, input.Length);
                    gzs.Close();
                    return outMs.ToArray();
                }
            }
        }


        /// <summary>
        /// 解压缩
        /// </summary>
        public static byte[] DeCompress(byte[] input)
        {
            using (MemoryStream inputMs = new MemoryStream(input))
            {
                using (MemoryStream outputMs = new MemoryStream())
                {
                    using (GZipStream gzs = new GZipStream(inputMs, CompressionMode.Decompress))
                    {
                        byte[] bytes = new byte[1024];
                        int len = 0;
                        while ((len = gzs.Read(bytes, 0, bytes.Length)) > 0)
                        {
                            outputMs.Write(bytes, 0, len);
                        }
                        gzs.Close();
                        return outputMs.ToArray();
                    }
                }
            }
        }


        #region LOG
        public enum LogColor
        {
            None,
            Red,
            Green,
            Blue,
            Cyan,
            Magenta,
            Yellow
        }

        public static Action<string> LogFunc;
        public static Action<LogColor, string> ColorLogFunc;
        public static Action<string> WarnFunc;
        public static Action<string> ErrorFunc;

        public static void Log(string msg, params object[] args)
        {
            msg = string.Format(msg, args);
            if (LogFunc != null)
            {
                LogFunc(msg);
            }
            else
            {
                ConsoleLog(msg, LogColor.None);
            }
        }
        public static void ColorLog(LogColor color, string msg, params object[] args)
        {
            msg = string.Format(msg, args);
            if (ColorLogFunc != null)
            {
                ColorLogFunc(color, msg);
            }
            else
            {
                ConsoleLog(msg, color);
            }
        }
        public static void Warning(string msg, params object[] args)
        {
            msg = string.Format(msg, args);
            if (WarnFunc != null)
            {
                WarnFunc(msg);
            }
            else
            {
                ConsoleLog(msg, LogColor.Yellow);
            }
        }
        public static void Error(string msg, params object[] args)
        {
            msg = string.Format(msg, args);
            if (ErrorFunc != null)
            {
                ErrorFunc(msg);
            }
            else
            {
                ConsoleLog(msg, LogColor.Red);
            }
        }
        private static void ConsoleLog(string msg, LogColor color)
        {
            int threadID = Thread.CurrentThread.ManagedThreadId;
            msg = string.Format("Thread:{0} {1}", threadID, msg);
            switch (color)
            {
                case LogColor.Red:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Green:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Blue:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Cyan:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Magenta:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.Yellow:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogColor.None:
                default:
                    Console.WriteLine(msg);
                    break;
            }
        }

        #endregion
    }
}
