using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class MpcGeneratorWindow : EditorWindow
{
    private const string PrefInput      = "MpcGen_InputPath";
    private const string PrefOutput     = "MpcGen_OutputPath";
    private const string PrefInputRel   = "MpcGen_InputIsRelative";
    private const string PrefOutputRel  = "MpcGen_OutputIsRelative";

    private string _inputPath   = "";
    private string _outputPath  = "";
    private bool   _inputRel    = true;
    private bool   _outputRel   = true;
    private string _log         = "";
    private Vector2 _scroll;
    private bool   _running     = false;

    [MenuItem("Tools/MPC Generator")]
    public static void Open() => GetWindow<MpcGeneratorWindow>("MPC Generator");

    private void OnEnable()
    {
        _inputPath  = EditorPrefs.GetString(PrefInput,     "");
        _outputPath = EditorPrefs.GetString(PrefOutput,    "");
        _inputRel   = EditorPrefs.GetBool  (PrefInputRel,  true);
        _outputRel  = EditorPrefs.GetBool  (PrefOutputRel, true);
    }

    private void DrawPathField(string label, ref string path, ref bool isRelative, string pathKey, string relKey)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        bool newRel = EditorGUILayout.ToggleLeft("Relative", isRelative, GUILayout.Width(72));
        if (newRel != isRelative)
        {
            isRelative = newRel;
            EditorPrefs.SetBool(relKey, isRelative);
        }

        string newPath = EditorGUILayout.TextField(path);
        if (newPath != path)
        {
            path = newPath;
            EditorPrefs.SetString(pathKey, path);
        }

        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string root    = ProjectRoot();
            string openAt  = isRelative ? Path.Combine(root, path) : path;
            if (!Directory.Exists(openAt)) openAt = root;

            string selected = EditorUtility.OpenFolderPanel(label, openAt, "");
            if (!string.IsNullOrEmpty(selected))
            {
                path = isRelative ? ToRelative(root, selected) : selected;
                EditorPrefs.SetString(pathKey, path);
                GUI.FocusControl(null);
                Repaint();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnGUI()
    {
        GUILayout.Label("MPC Code Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        DrawPathField("Input Path",  ref _inputPath,  ref _inputRel,  PrefInput,  PrefInputRel);
        DrawPathField("Output Path", ref _outputPath, ref _outputRel, PrefOutput, PrefOutputRel);

        EditorGUILayout.Space(8);

        EditorGUI.BeginDisabledGroup(_running);
        if (GUILayout.Button("Run MPC Generation", GUILayout.Height(32)))
            RunGeneration();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(6);
        GUILayout.Label("Log", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunGeneration()
    {
        _log    = "";
        _running = true;
        Repaint();

        string root      = ProjectRoot();
        string inputAbs  = _inputRel  ? Path.GetFullPath(Path.Combine(root, _inputPath))  : _inputPath;
        string outputAbs = _outputRel ? Path.GetFullPath(Path.Combine(root, _outputPath)) : _outputPath;

        AppendLog(">>> dotnet tool install -g MessagePack.Generator --version 2.5.187");
        var (_, installOut) = Exec("dotnet tool install -g MessagePack.Generator --version 2.5.187");
        AppendLog(installOut);

        string mpcCmd = $"mpc -i \"{inputAbs}\" -o \"{outputAbs}\"";
        AppendLog($">>> {mpcCmd}");
        var (code, mpcOut) = Exec(mpcCmd);
        AppendLog(mpcOut);
        AppendLog(code == 0 ? "✓ Done!" : $"✗ mpc exited with code {code}");

        _running = false;
        Repaint();
    }

    private static (int code, string output) Exec(string command)
    {
        bool isWin = Application.platform == RuntimePlatform.WindowsEditor;
        string escaped = command.Replace("\"", isWin ? "\\\"" : "\\\"");

        var psi = new ProcessStartInfo
        {
            FileName               = isWin ? "cmd.exe" : "/bin/bash",
            Arguments              = isWin ? $"/c \"{escaped}\"" : $"-c \"{escaped}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        var sb = new StringBuilder();
        using var p = Process.Start(psi);
        sb.Append(p.StandardOutput.ReadToEnd());
        sb.Append(p.StandardError.ReadToEnd());
        p.WaitForExit();
        return (p.ExitCode, sb.ToString().Trim());
    }

    private void AppendLog(string text)
    {
        if (!string.IsNullOrEmpty(text))
            _log += text + "\n";
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static string ToRelative(string root, string full)
    {
        root = root.Replace('\\', '/').TrimEnd('/') + '/';
        full = full.Replace('\\', '/');
        return full.StartsWith(root) ? full.Substring(root.Length) : full;
    }
}
