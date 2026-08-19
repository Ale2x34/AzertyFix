using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Windows.Forms;

[assembly: AssemblyTitle("AzertyFix")]
[assembly: AssemblyProduct("AzertyFix pour MECCHA CHAMELEON")]
[assembly: AssemblyDescription("Remappe ZQSD sur WASD dans MECCHA CHAMELEON")]
[assembly: AssemblyCompany("Ale2x_")]
[assembly: AssemblyCopyright("Développé par Ale2x_")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class AzertyFix
{
    const string AUTHOR = "Développé par Ale2x_";
    const string VERSION = "1.1";

    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    const uint LLKHF_INJECTED = 0x10;
    const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;
    const uint MAPVK_VK_TO_VSC = 0;
    const uint INPUT_KEYBOARD = 1;
    static readonly IntPtr TAG = new IntPtr(0x415A4659);

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion U; }

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint uCode, uint uMapType);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);

    const string APP_NAME = "AzertyFix-Meccha";
    const string REG_KEY = @"Software\Ale2x_\AzertyFix";
    const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const int VK_TOGGLE = 0x78;
    const int VK_LOG = 0x79;

    static IntPtr _hook = IntPtr.Zero;
    static HookProc _proc;
    static Mutex _mutex;
    static bool _enabled = true;
    static bool _scancodeMode = false;
    static bool _logging = false;
    static NotifyIcon _tray;
    static ToolStripMenuItem _miEnabled, _miScan, _miLog, _miStartup;
    static string _logPath;
    static readonly object _logLock = new object();

    static uint _lastPid = 0;
    static bool _lastPidIsGame = false;
    static string _lastPidName = "?";

    static readonly string[] GameProcessPrefixes = { "PenguinHotel", "Chameleon" };
    const string GameWindowTitle = "Chameleon";

    static readonly Dictionary<uint, ushort> Map = new Dictionary<uint, ushort>
    {
        { 0x41, 0x51 },
        { 0x51, 0x41 },
        { 0x5A, 0x57 },
        { 0x57, 0x5A },
    };

    [STAThread]
    static void Main()
    {
        bool first;
        _mutex = new Mutex(true, "AzertyFix_Ale2x_SingleInstance", out first);
        if (!first)
        {
            MessageBox.Show("AzertyFix est déjà lancé.\nRegarde l'icône près de l'horloge.",
                            "AzertyFix", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        _logPath = ResolveLogPath();
        LoadSettings();

        _miEnabled = new ToolStripMenuItem("Remap actif (F9)", null, delegate { Toggle(); });
        _miEnabled.Checked = _enabled;
        _miScan = new ToolStripMenuItem("Mode scancode (si le remap reste sans effet)", null, delegate { ToggleScan(); });
        _miScan.Checked = _scancodeMode;
        _miLog = new ToolStripMenuItem("Journal de diagnostic (F10)", null, delegate { ToggleLog(); });
        _miLog.Checked = _logging;
        _miStartup = new ToolStripMenuItem("Lancer au démarrage de Windows", null, delegate { ToggleStartup(); });
        _miStartup.Checked = IsStartupEnabled();

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("AzertyFix " + VERSION + " — MECCHA CHAMELEON") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem(AUTHOR) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_miEnabled);
        menu.Items.Add(_miScan);
        menu.Items.Add(_miStartup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_miLog);
        menu.Items.Add(new ToolStripMenuItem("Ouvrir le journal", null, delegate { OpenLog(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("À propos", null, delegate { About(); }));
        menu.Items.Add(new ToolStripMenuItem("Quitter", null, delegate { Application.Exit(); }));

        _tray = new NotifyIcon();
        _tray.Icon = LoadIcon();
        _tray.Text = "AzertyFix — MECCHA CHAMELEON | par Ale2x_";
        _tray.ContextMenuStrip = menu;
        _tray.Visible = true;
        _tray.DoubleClick += delegate { Toggle(); };

        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            MessageBox.Show("Impossible d'installer le hook clavier (erreur " + Marshal.GetLastWin32Error() + ").",
                            "AzertyFix", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Log("=== Démarrage — hook OK — version " + VERSION + " ===");

        Balloon("AzertyFix actif — par Ale2x_",
                "ZQSD joue comme WASD dans MECCHA CHAMELEON.\nF9 pour désactiver (chat).");
        Application.ApplicationExit += delegate
        {
            if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
            if (_tray != null) _tray.Visible = false;
        };
        Application.Run();
    }

    static string ResolveLogPath()
    {
        try
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string probe = Path.Combine(dir, "azertyfix.tmp");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return Path.Combine(dir, "log.txt");
        }
        catch
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzertyFix");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "log.txt");
        }
    }

    static void Log(string s)
    {
        if (!_logging) return;
        try
        {
            lock (_logLock)
                File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", new UTF8Encoding(true));
        }
        catch { }
    }

    static void ToggleLog()
    {
        _logging = !_logging;
        _miLog.Checked = _logging;
        SaveSettings();
        if (_logging)
        {
            try { File.WriteAllText(_logPath, "=== AzertyFix " + VERSION + " — " + AUTHOR + " ===\r\n", new UTF8Encoding(true)); } catch { }
            Log("Journal activé.");
            Balloon("Journal activé", "Écrit dans " + _logPath);
        }
        else Balloon("Journal désactivé", "Plus rien n'est écrit sur le disque.");
    }

    static void OpenLog()
    {
        if (!File.Exists(_logPath))
        {
            MessageBox.Show("Aucun journal pour l'instant.\nActive « Journal de diagnostic » puis retente.",
                            "AzertyFix", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { Process.Start("notepad.exe", _logPath); } catch { }
    }

    static void LoadSettings()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(REG_KEY, false))
            {
                if (k == null) return;
                _scancodeMode = "1".Equals(Convert.ToString(k.GetValue("Scancode", "0")));
                _logging = "1".Equals(Convert.ToString(k.GetValue("Logging", "0")));
            }
        }
        catch { }
    }

    static void SaveSettings()
    {
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey(REG_KEY))
            {
                if (k == null) return;
                k.SetValue("Scancode", _scancodeMode ? "1" : "0");
                k.SetValue("Logging", _logging ? "1" : "0");
            }
        }
        catch { }
    }

    static Icon LoadIcon()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false))
            {
                if (k != null)
                {
                    string steam = Convert.ToString(k.GetValue("SteamPath", ""));
                    if (!string.IsNullOrEmpty(steam))
                    {
                        string ico = Path.Combine(steam.Replace('/', '\\'),
                            @"steam\games\fbef288370f6ae51cb0a5066d7fb13d26147c94a.ico");
                        if (File.Exists(ico)) return new Icon(ico, 32, 32);
                    }
                }
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    static void About()
    {
        MessageBox.Show(
            "AzertyFix " + VERSION + " pour MECCHA CHAMELEON\r\n" + AUTHOR + "\r\n\r\n" +
            "Le jeu n'a aucun menu de remappage clavier. Cet outil inverse A/Q et Z/W\r\n" +
            "uniquement quand la fenêtre du jeu est au premier plan : ZQSD joue comme\r\n" +
            "WASD, et le reste de Windows garde un AZERTY normal.\r\n\r\n" +
            "F9  — activer / désactiver le remap (pour taper dans le chat)\r\n" +
            "F10 — ouvrir le journal de diagnostic\r\n\r\n" +
            "Aucune donnée n'est envoyée nulle part. Le journal est désactivé par\r\n" +
            "défaut, reste sur ce PC, et ne note jamais ce qui se passe hors du jeu.\r\n" +
            "Le code source complet est fourni dans AzertyFix.cs.",
            "À propos d'AzertyFix", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static void Toggle()
    {
        _enabled = !_enabled; _miEnabled.Checked = _enabled;
        Log("Remap " + (_enabled ? "activé" : "désactivé"));
        Balloon(_enabled ? "Remap actif" : "Remap désactivé",
                _enabled ? "ZQSD joue comme WASD." : "Clavier AZERTY normal (chat).");
    }

    static void ToggleScan()
    {
        _scancodeMode = !_scancodeMode; _miScan.Checked = _scancodeMode;
        SaveSettings();
        Log("Mode scancode = " + _scancodeMode);
        Balloon("Mode d'injection", _scancodeMode ? "Scancode" : "Touche virtuelle");
    }

    static void Balloon(string t, string x)
    { try { _tray.BalloonTipTitle = t; _tray.BalloonTipText = x; _tray.ShowBalloonTip(2500); } catch { } }

    static bool IsStartupEnabled()
    {
        try { using (var k = Registry.CurrentUser.OpenSubKey(RUN_KEY, false)) return k != null && k.GetValue(APP_NAME) != null; }
        catch { return false; }
    }

    static void ToggleStartup()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RUN_KEY, true))
            {
                if (k == null) return;
                if (IsStartupEnabled()) { k.DeleteValue(APP_NAME, false); _miStartup.Checked = false; }
                else { k.SetValue(APP_NAME, "\"" + Application.ExecutablePath + "\""); _miStartup.Checked = true; }
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "AzertyFix"); }
    }

    static bool GameHasFocus()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        uint pid; GetWindowThreadProcessId(hwnd, out pid);
        if (pid == 0) return false;
        if (pid == _lastPid) return _lastPidIsGame;

        _lastPid = pid; _lastPidIsGame = false; _lastPidName = "?";
        try
        {
            using (var p = Process.GetProcessById((int)pid))
            {
                _lastPidName = p.ProcessName;
                foreach (string g in GameProcessPrefixes)
                    if (_lastPidName.StartsWith(g, StringComparison.OrdinalIgnoreCase)) { _lastPidIsGame = true; break; }
            }
        }
        catch { }

        if (!_lastPidIsGame)
        {
            var sb = new StringBuilder(256); GetWindowText(hwnd, sb, sb.Capacity);
            if (sb.ToString().Trim().StartsWith(GameWindowTitle, StringComparison.OrdinalIgnoreCase))
                _lastPidIsGame = true;
        }

        Log(_lastPidIsGame
            ? "Fenêtre active = le jeu (" + _lastPidName + ") — remap opérationnel"
            : "Fenêtre active = autre application — remap en veille");
        return _lastPidIsGame;
    }

    static void Send(ushort vk, bool up)
    {
        ushort scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        var inp = new INPUT[1];
        inp[0].type = INPUT_KEYBOARD;
        inp[0].U.ki.wVk = _scancodeMode ? (ushort)0 : vk;
        inp[0].U.ki.wScan = scan;
        inp[0].U.ki.dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (_scancodeMode ? KEYEVENTF_SCANCODE : 0);
        inp[0].U.ki.time = 0;
        inp[0].U.ki.dwExtraInfo = TAG;
        if (SendInput(1, inp, Marshal.SizeOf(typeof(INPUT))) == 0)
            Log("  !! SendInput refusé, erreur " + Marshal.GetLastWin32Error()
                + " (le jeu tourne-t-il en administrateur ?)");
    }

    static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hook, nCode, wParam, lParam);

        int msg = (int)wParam;
        bool isDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
        bool isUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);
        if (!isDown && !isUp) return CallNextHookEx(_hook, nCode, wParam, lParam);

        var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

        if (kb.dwExtraInfo == TAG) return CallNextHookEx(_hook, nCode, wParam, lParam);
        if ((kb.flags & LLKHF_INJECTED) != 0) return CallNextHookEx(_hook, nCode, wParam, lParam);

        if (isDown && kb.vkCode == VK_TOGGLE) { Toggle(); return CallNextHookEx(_hook, nCode, wParam, lParam); }
        if (isDown && kb.vkCode == VK_LOG) { OpenLog(); return CallNextHookEx(_hook, nCode, wParam, lParam); }

        ushort target;
        if (!Map.TryGetValue(kb.vkCode, out target)) return CallNextHookEx(_hook, nCode, wParam, lParam);

        if (_enabled && GameHasFocus())
        {
            Log("  " + (char)kb.vkCode + (isDown ? " enfoncée" : " relâchée") + " → envoie " + (char)target);
            Send(target, isUp);
            return (IntPtr)1;
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
