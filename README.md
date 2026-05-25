# KCPNet

基于 [KCP 协议](https://github.com/skywind3000/kcp)的 .NET UDP 通信库，提供可靠、低延迟的数据传输，适用于对实时性要求较高的场景（如游戏、实时通信）。

## 项目结构

```
KCPNet/               — 核心库（netstandard2.0）
  KCPNet.cs           — 泛型宿主 KCPNet<T,K>：UDP 传输、服务端/客户端模式、会话管理
  KCPSession.cs       — 抽象会话 KCPSession<T>：KCP 实例生命周期、收发管道、更新循环
  KCPHandle.cs        — IKcpCallback 适配器，连接 Kcp 库与委托回调
  KCPTool.cs          — 工具类：MessagePack 序列化、GZip 压缩、彩色控制台日志

KCPExampleProtocol/   — 共享协议定义（netstandard2.0）
  NetMessage.cs           — NetMessage、NetPing、CMD 枚举

KCPExampleClient/     — 示例客户端（net8.0）
  ClientStart.cs      — 入口，连接重试循环，5 秒心跳
  ClientSession.cs    — KCPSession<NetMessage> 实现

KCPExampleServer/     — 示例服务端（net8.0）
  ServerStart.cs      — 入口，支持从 stdin 广播消息
  ServerSession.cs    — KCPSession<NetMessage> 实现，含 ping 超时检测

UnityPackage/         — Unity 预编译集成文件
  Plugins/            — KCPNet、Kcp、MessagePack 等 DLL
  Scripts/            — MessagePackInitializer（自动初始化 + 日志接管）
```

## 构建与运行

```bash
# 构建所有项目
dotnet build

# 启动服务端
dotnet run --project KCPExampleServer

# 启动客户端（另开终端）
dotnet run --project KCPExampleClient
```

客户端连接成功后，可在服务端终端输入文本并回车，消息将广播给所有已连接的客户端。输入 `quit` 退出。

## 核心用法

### 定义消息

```csharp
[MessagePackObject]
public class MyMessage : KCPMessage
{
    [Key(0)] public int id;
    [Key(1)] public string text;
}
```

所有消息类必须继承 `KCPMessage`，标注 `[MessagePackObject]`，字段标注 `[Key(n)]`。

### 继承 KCPSession

```csharp
class MySession : KCPSession<MyMessage>
{
    protected override void OnConnected() { }
    protected override void OnDisconnected() { }
    protected override void OnReceiveMessage(MyMessage msg) { }
    protected override void OnUpdate(DateTime now) { }
}
```

### 启动服务端

```csharp
var server = new KCPNet<MySession, MyMessage>();
server.StartAsServer("0.0.0.0", 17666);

// 广播消息
server.BroadcastMessage(new MyMessage { text = "hello all" });
```

### 启动客户端

```csharp
var client = new KCPNet<MySession, MyMessage>();
client.StartAsClient("127.0.0.1", 17666);
bool ok = await client.ConnectServer(200, 5000);

// 发送消息
client.clientSession.SendMessage(new MyMessage { text = "hello" });
```

## 架构说明

**传输层** (`KCPNet.cs`)：泛型类 `KCPNet<T, K>`，在 `Task.Run` 上运行 UDP 接收循环。服务端分配唯一 session ID；客户端通过发送 4 字节零值握手，接收 8 字节响应（含 session ID）完成连接。

**会话层** (`KCPSession.cs`)：每个对端持有一个 `Kcp<KcpSegment>` 实例，运行于极速模式（`NoDelay(1,10,2,1)`，窗口 64，MTU 512）。后台更新循环每 10ms 调用一次 `_kcp.Update()`，并排空接收队列。子类重写 `OnReceiveMessage`、`OnUpdate`、`OnConnected`、`OnDisconnected`。

**消息管道**：
- 发送：`SendMessage` → MessagePack 序列化 → GZip 压缩 → `_kcp.Send()` → UDP 发出
- 接收：UDP → `_kcp.Input()` → `_kcp.Recv()` → GZip 解压 → MessagePack 反序列化 → `OnReceiveMessage`

**连接握手**：客户端发送 `[0,0,0,0]`，服务端生成唯一 uint 并回复 `[0,0,0,0,sid_bytes]`，客户端提取 sid 后创建本地会话。后续 UDP 包均以 4 字节 session ID 为前缀。

## 在 Unity 中使用

核心库目标 `netstandard2.0`，完全兼容 Unity。`UnityPackage/` 目录下包含预编译 DLL，开箱即用。

### 快速集成

1. 将 `UnityPackage/Plugins/` 下所有 DLL 复制到 Unity 项目的 `Assets/Plugins/`
2. 将 `UnityPackage/Scripts/MessagePackInitializer.cs` 复制到 `Assets/Scripts/`
   - 该脚本通过 `[RuntimeInitializeOnLoadMethod]` 在场景加载前自动初始化 MessagePack 并将 KCPNet 日志接管到 `Debug.Log`

### 定义消息

```csharp
using KCPNet;
using MessagePack;

[MessagePackObject]
public class GameMessage : KCPMessage
{
    [Key(0)] public CMD cmd;
    [Key(1)] public string content;
    [Key(2)] public float x;
    [Key(3)] public float y;
}

public enum CMD
{
    None, Move, Attack, Chat,
}
```

Unity 原生类型（如 `Vector3`）不受 MessagePack 原生支持，如需传递坐标等数据，应自定义可序列化的数据结构。

### 继承会话类

```csharp
using KCPNet;
using UnityEngine;

public class GameSession : KCPSession<GameMessage>
{
    protected override void OnConnected()
    {
        Debug.Log($"连接服务器成功，sid:{sId}");
    }

    protected override void OnDisconnected()
    {
        Debug.Log("与服务器断开连接");
    }

    protected override void OnReceiveMessage(GameMessage msg)
    {
        Debug.Log($"收到消息 cmd:{msg.cmd} content:{msg.content}");
    }

    protected override void OnUpdate(DateTime now) { }
}
```

### 客户端 MonoBehaviour

```csharp
using KCPNet;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    private KCPNet<GameSession, GameMessage> _client;

    private void Start()
    {
        _client = new KCPNet<GameSession, GameMessage>();
        _client.StartAsClient("127.0.0.1", 17666);
        _client.ConnectServer(200, 5000).ContinueWith(task =>
        {
            if (task.Result)
                Debug.Log("连入服务器成功");
            else
                Debug.LogError("连入服务器失败");
        });
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _client?.clientSession?.SendMessage(new GameMessage
            {
                cmd = CMD.Chat,
                content = "Hello from Unity!"
            });
        }
    }

    private void OnDestroy()
    {
        _client?.CloseClient();
    }
}
```

### 服务端也可以在 Unity 中运行

适合本地联机调试、LAN 对战等场景：

```csharp
private KCPNet<GameSession, GameMessage> _server;

private void Start()
{
    _server = new KCPNet<GameSession, GameMessage>();
    _server.StartAsServer("0.0.0.0", 17666);
}

private void OnDestroy()
{
    _server?.CloseServer();
}
```

生产环境建议将服务端部署为独立控制台程序（参考 `KCPExampleServer`），Unity 端只做客户端。

### IL2CPP 构建

使用 IL2CPP 构建（iOS、Xbox、部分 Android）时，需运行 MessagePack 代码生成器生成预编译解析器：

```bash
dotnet tool install -g MessagePack.Generator --version 2.5.187
mpc -i ./YourProtocolProject -o ./Unity/Assets/Scripts/Generated
```

然后在 `MessagePackInitializer.cs` 中取消注释 IL2CPP 代码块，将 `StandardResolver` 替换为 `GeneratedResolver`。

### MPC 代码生成器编辑器窗口

`UnityPackage/Editor/MpcGeneratorWindow.cs` 提供了一个 Unity Editor 窗口，可在编辑器内直接运行 MessagePack 代码生成器，无需手动敲命令行：

- 打开方式：**Tools → MPC Generator**
- 设置输入路径（协议项目目录）和输出路径（生成代码存放目录），支持相对/绝对路径切换
- 点击 **Run MPC Generation** 自动执行 `dotnet tool install`（如未安装）和 `mpc` 命令
- 日志实时显示在窗口下方的日志区域

## 依赖

- [Kcp](https://www.nuget.org/packages/Kcp) v2.7.0（`System.Net.Sockets.Kcp` 命名空间）
- [MessagePack](https://www.nuget.org/packages/MessagePack) v2.5.187
