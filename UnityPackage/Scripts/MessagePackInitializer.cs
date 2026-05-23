using UnityEngine;
using MessagePack;
using MessagePack.Resolvers;

namespace KCPNet.Unity
{
    /// <summary>
    /// 在场景加载前自动初始化 MessagePack 并接管 KCPTool 日志输出。
    /// 无需手动挂载，[RuntimeInitializeOnLoadMethod] 会自动执行。
    ///
    /// IL2CPP 说明：发布 IL2CPP 构建前需运行 mpc 生成 GeneratedResolver，
    /// 然后取消注释下方 IL2CPP 代码块。Mono / Editor 无需任何额外操作。
    /// </summary>
    public class MessagePackInitializer : MonoBehaviour
    {
        private static bool _initialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (_initialized) return;

            GameObject host = new GameObject("[KCPNet]");
            DontDestroyOnLoad(host);
            host.AddComponent<MessagePackInitializer>();
        }

        private void Awake()
        {
            if (_initialized)
            {
                Destroy(this);
                return;
            }
            _initialized = true;

            InitMessagePack();
            HookLogging();
        }

        private static void InitMessagePack()
        {
#if !ENABLE_IL2CPP
            // Mono / Editor：反射解析，无需代码生成
            StaticCompositeResolver.Instance.Register(
                StandardResolver.Instance
            );
#else
            // IL2CPP：需要先运行 mpc 生成 GeneratedResolver
            // 安装：dotnet tool install -g MessagePack.Generator --version 2.5.187
            // 生成：mpc -i ./KCPExampleProtocol -o ./UnityPackage/Scripts/Generated
            // 生成后取消下方注释，并删除 StandardResolver 那行：
            //
            // StaticCompositeResolver.Instance.Register(
            //     GeneratedResolver.Instance,
            //     StandardResolver.Instance
            // );

            StaticCompositeResolver.Instance.Register(
                StandardResolver.Instance
            );
            Debug.LogWarning("[KCPNet] IL2CPP 构建未注册 GeneratedResolver，请运行 mpc 并取消注释 IL2CPP 代码块。");
#endif
            MessagePackSerializer.DefaultOptions =
                MessagePackSerializerOptions.Standard
                    .WithResolver(StaticCompositeResolver.Instance);

            Debug.Log("[KCPNet] MessagePack 初始化完成。");
        }

        private static void HookLogging()
        {
            KCPTool.LogFunc = msg => Debug.Log("[KCPNet] " + msg);
            KCPTool.WarnFunc = msg => Debug.LogWarning("[KCPNet] " + msg);
            KCPTool.ErrorFunc = msg => Debug.LogError("[KCPNet] " + msg);
            KCPTool.ColorLogFunc = (color, msg) =>
            {
                switch (color)
                {
                    case KCPTool.LogColor.Red:
                        Debug.LogError("[KCPNet] " + msg);
                        break;
                    case KCPTool.LogColor.Yellow:
                        Debug.LogWarning("[KCPNet] " + msg);
                        break;
                    default:
                        Debug.Log("[KCPNet] " + msg);
                        break;
                }
            };
        }

        private void OnDestroy()
        {
            // Editor 退出 Play Mode 时重置，确保下次进入 Play Mode 能重新初始化
            _initialized = false;
        }
    }
}
