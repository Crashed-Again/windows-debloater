using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Cub
{
    internal enum LogKind { Info, Step, Ok, Warn }

    /// <summary>Everything the user picked in the UI, captured when Apply is pressed.</summary>
    internal sealed class Options
    {
        public HashSet<string> On = new HashSet<string>();
        public List<string> AppPatterns = new List<string>();
        public List<string> Pins = new List<string>();
        public string BrowserId = "";
        public string BrowserName = "";
        public string WallpaperPath = "";
        public string ExtraIds = "";

        public bool Has(string key) { return On.Contains(key); }
    }

    internal static class Native
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SpiString(int action, int param, string value, int winIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        public static extern bool SpiInts(int action, int param, int[] value, int winIni);
    }

    internal sealed class Engine
    {
        private readonly Options o;
        private readonly Action<string, LogKind> log;
        private readonly Action<double, string> progress;

        private static readonly RegistryKey HKLM = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        private static readonly RegistryKey HKCU = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        private const string Adv = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        public Engine(Options options, Action<string, LogKind> logger, Action<double, string> progressCallback)
        {
            o = options;
            log = logger;
            progress = progressCallback;
        }

        // ------------------------------------------------------------ logging
        private void Info(string m) { log(m, LogKind.Info); }
        private void Ok(string m) { log(m, LogKind.Ok); }
        private void Warn(string m) { log(m, LogKind.Warn); }

        private void Echo(string line)
        {
            string t = line.Trim();
            if (t.Length < 2) return;
            if (t.IndexOf('\u2588') >= 0 || t.IndexOf('\u2592') >= 0) return;        // winget progress bars
            if (Regex.IsMatch(t, @"^[-\\|/\s]+$")) return;                              // winget spinner
            if (t.StartsWith("[ok]")) Ok(t.Substring(4).Trim());
            else if (t.StartsWith("[!!]")) Warn(t.Substring(4).Trim());
            else Info(t);
        }

        // ------------------------------------------------------------ registry
        private void SetReg(RegistryKey hive, string path, string name, object value,
                            RegistryValueKind kind = RegistryValueKind.DWord)
        {
            try
            {
                using (RegistryKey k = hive.CreateSubKey(path, true))
                    k.SetValue(name, value, kind);
            }
            catch (Exception e)
            {
                Warn("Registry write failed: " + path + "\\" + name + " (" + e.Message + ")");
            }
        }

        private void SetStr(RegistryKey hive, string path, string name, string value)
        {
            SetReg(hive, path, name, value, RegistryValueKind.String);
        }

        // ------------------------------------------------------------ processes
        private int Exec(string file, string args, bool echo, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                var sb = new StringBuilder();
                using (Process p = Process.Start(psi))
                {
                    DataReceivedEventHandler h = (s, e) =>
                    {
                        if (e.Data == null) return;
                        lock (sb) { sb.AppendLine(e.Data); }
                        if (echo) Echo(e.Data);
                    };
                    p.OutputDataReceived += h;
                    p.ErrorDataReceived += h;
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    output = sb.ToString();
                    return p.ExitCode;
                }
            }
            catch (Exception e)
            {
                output = e.Message;
                return -1;
            }
        }

        private int Run(string file, string args)
        {
            string ignored;
            return Exec(file, args, false, out ignored);
        }

        private int PS(string script)
        {
            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            string ignored;
            return Exec("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + enc, true, out ignored);
        }

        // ------------------------------------------------------------ winget
        private bool WingetAvailable()
        {
            string ignored;
            return Exec("where.exe", "winget", false, out ignored) == 0;
        }

        private bool InstallOrUpdate(string id, string name, bool allowUpdate = true)
        {
            if (!WingetAvailable())
            {
                Warn("winget not found - cannot install " + name);
                return false;
            }
            const string common = "--exact --silent --accept-package-agreements --accept-source-agreements";
            string ignored;
            if (Exec("winget", "list --id " + id + " --exact --accept-source-agreements", false, out ignored) == 0)
            {
                if (allowUpdate)
                {
                    Info(name + " is installed - checking for updates...");
                    Exec("winget", "upgrade --id " + id + " " + common, true, out ignored);   // "no update available" is fine
                }
                else
                {
                    Info(name + " is already installed - skipping the update check.");
                }
                return true;
            }
            Info("Installing " + name + "...");
            Exec("winget", "install --id " + id + " " + common, true, out ignored);
            return Exec("winget", "list --id " + id + " --exact --accept-source-agreements", false, out ignored) == 0;
        }

        // ------------------------------------------------------------ main sequence
        public void Run()
        {
            var steps = new List<KeyValuePair<string, Action>>();
            Action<string, Action> add = (title, a) => steps.Add(new KeyValuePair<string, Action>(title, a));

            if (o.Has("restore")) add("Creating restore point", RestorePoint);

            // look & feel
            if (o.Has("dark")) add("Dark mode", ApplyDarkMode);
            if (o.Has("left")) add("Taskbar icons to the left", () => { SetReg(HKCU, Adv, "TaskbarAl", 0); Ok("Taskbar aligned left"); });
            if (o.Has("thispc")) add("File Explorer opens to This PC", () => { SetReg(HKCU, Adv, "LaunchTo", 1); Ok("Explorer opens to This PC"); });
            if (o.Has("fileext")) add("Show file extensions", () => { SetReg(HKCU, Adv, "HideFileExt", 0); Ok("File extensions visible"); });
            if (o.Has("classicmenu")) add("Classic right-click menu", ClassicMenu);
            if (o.Has("wallpaper")) add("Wallpaper", Wallpaper);
            if (o.Has("mouse")) add("Mouse acceleration off", MouseAccel);
            if (o.Has("sticky")) add("Sticky Keys off", StickyKeys);

            // AI
            if (o.Has("copilot")) add("Removing Copilot", Copilot);
            if (o.Has("recall")) add("Disabling Recall / Click to Do", Recall);
            if (o.Has("websearch")) add("Turning off web results in search", () =>
            {
                SetReg(HKCU, @"SOFTWARE\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);
                Ok("Search stays local");
            });

            // apps
            if (o.Has("removeapps") && o.AppPatterns.Count > 0) add("Removing selected apps", () => RemoveAppx(o.AppPatterns.ToArray()));
            if (o.Has("suggest")) add("Blocking suggested apps and ads", Suggestions);

            // start menu
            if (o.Has("hiderec")) add("Start menu: hide Recommended", HideRecommended);
            if (o.Has("grid")) add("Start menu: grid view", () => { SetReg(HKCU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Start", "AllAppsViewMode", 1); Ok("All apps set to grid view"); });
            if (o.Has("pins")) add("Start menu: pinned apps", StartPins);

            // privacy & security
            if (o.Has("telemetry")) add("Disabling telemetry", Telemetry);
            if (o.Has("location")) add("Disabling location tracking", Location);
            if (o.Has("sac")) add("Disabling Smart App Control", () =>
            {
                SetReg(HKLM, @"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState", 0);
                Ok("Smart App Control off (takes effect after reboot)");
            });
            if (o.Has("bitlocker")) add("Disabling BitLocker / device encryption", BitLocker);
            if (o.Has("hibernate")) add("Disabling hibernation", () => { Run("powercfg", "/hibernate off"); Ok("Hibernation off (Fast Startup too)"); });

            // performance
            if (o.Has("ultimate")) add("Ultimate Performance power plan", PowerPlan);

            // software
            if (o.Has("quicklook")) add("Installing QuickLook (Space = preview)", () =>
            {
                if (InstallOrUpdate("QL-Win.QuickLook", "QuickLook")) Ok("QuickLook ready"); else Warn("QuickLook not installed");
            });

            string[] extras = (o.ExtraIds ?? "").Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string id in extras)
            {
                string captured = id;
                add("Installing " + captured, () =>
                {
                    if (InstallOrUpdate(captured, captured)) Ok(captured + " ready"); else Warn(captured + " failed");
                });
            }

            if (steps.Count > 0) add("Restarting Explorer", RestartExplorer);

            // LAST: browser
            bool keepEdge = o.BrowserId == "Microsoft.Edge";
            if (!string.IsNullOrEmpty(o.BrowserId))
                add("Browser: " + o.BrowserName, () => Browser(keepEdge));

            if (steps.Count == 0)
            {
                Warn("Nothing is switched on - nothing to do.");
                progress(1, "Nothing to do");
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                log(steps[i].Key, LogKind.Step);
                progress((double)i / steps.Count, steps[i].Key + "...");
                try { steps[i].Value(); }
                catch (Exception e) { Warn("Failed: " + e.Message); }
            }
            progress(1, "Finished");
        }

        // ------------------------------------------------------------ steps
        private void RestorePoint()
        {
            SetReg(HKLM, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", "SystemRestorePointCreationFrequency", 0);
            int code = PS("Enable-ComputerRestore -Drive \"$env:SystemDrive\\\"; Checkpoint-Computer -Description 'Before Cub' -RestorePointType MODIFY_SETTINGS");
            if (code == 0) Ok("Restore point created"); else Warn("Could not create a restore point");
        }

        private void ApplyDarkMode()
        {
            const string pers = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            SetReg(HKCU, pers, "AppsUseLightTheme", 0);
            SetReg(HKCU, pers, "SystemUsesLightTheme", 0);
            Ok("Apps and system set to dark");
        }

        private void ClassicMenu()
        {
            SetStr(HKCU, @"SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "");
            Ok("Classic right-click menu enabled (after Explorer restarts)");
        }

        private void Wallpaper()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            Directory.CreateDirectory(pictures);
            string wp;
            if (!string.IsNullOrEmpty(o.WallpaperPath) && File.Exists(o.WallpaperPath))
            {
                wp = Path.Combine(pictures, "Cub-wallpaper" + Path.GetExtension(o.WallpaperPath));
                File.Copy(o.WallpaperPath, wp, true);
            }
            else
            {
                wp = Path.Combine(pictures, "Cub-wallpaper.png");
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("wallpaper.png"))
                using (FileStream f = File.Create(wp))
                    s.CopyTo(f);
            }
            const string desk = @"Control Panel\Desktop";
            SetStr(HKCU, desk, "WallpaperStyle", "10");   // Fill
            SetStr(HKCU, desk, "TileWallpaper", "0");
            SetStr(HKCU, desk, "Wallpaper", wp);
            Native.SpiString(20, 0, wp, 3);               // SPI_SETDESKWALLPAPER
            Ok("Wallpaper set (" + wp + ")");
        }

        private void MouseAccel()
        {
            const string mouse = @"Control Panel\Mouse";
            SetStr(HKCU, mouse, "MouseSpeed", "0");
            SetStr(HKCU, mouse, "MouseThreshold1", "0");
            SetStr(HKCU, mouse, "MouseThreshold2", "0");
            Native.SpiInts(4, 0, new int[] { 0, 0, 0 }, 3);   // SPI_SETMOUSE
            Ok("Pointer precision (acceleration) off");
        }

        private void StickyKeys()
        {
            const string acc = @"Control Panel\Accessibility";
            SetStr(HKCU, acc + @"\StickyKeys", "Flags", "506");
            SetStr(HKCU, acc + @"\ToggleKeys", "Flags", "58");
            SetStr(HKCU, acc + @"\Keyboard Response", "Flags", "122");
            Ok("Sticky / Toggle / Filter Keys shortcuts disabled");
        }

        private void Copilot()
        {
            SetReg(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            SetReg(HKCU, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            SetReg(HKCU, Adv, "ShowCopilotButton", 0);
            RemoveAppx(new[] { "Microsoft.Copilot", "Microsoft.Windows.Ai.Copilot.Provider", "MicrosoftWindows.Client.CoPilot", "Microsoft.549981C3F5F10" });
            Ok("Copilot disabled and removed");
        }

        private void Recall()
        {
            SetReg(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
            SetReg(HKCU, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
            SetReg(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1);
            PS("try { Disable-WindowsOptionalFeature -Online -FeatureName 'Recall' -NoRestart -ErrorAction Stop | Out-Null } catch { }");
            Ok("Recall and Click to Do disabled");
        }

        private void RemoveAppx(string[] names)
        {
            string list = string.Join(",", names.Select(n => "'" + n.Replace("'", "''") + "'"));
            string script =
                "$names = @(" + list + ");" +
                "$prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue;" +
                "foreach ($p in $names) {" +
                "  $f = Get-AppxPackage -AllUsers -Name $p -ErrorAction SilentlyContinue;" +
                "  if ($f) { $f | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Write-Host \"[ok] Removed $p\" }" +
                "  $prov | Where-Object { $_.DisplayName -like $p } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null }" +
                "}";
            PS(script);
        }

        private void Suggestions()
        {
            const string cdm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            string[] keys =
            {
                "SilentInstalledAppsEnabled", "ContentDeliveryAllowed", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled",
                "PreInstalledAppsEverEnabled", "SystemPaneSuggestionsEnabled", "SoftLandingEnabled",
                "SubscribedContent-310093Enabled", "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled",
                "SubscribedContent-338389Enabled", "SubscribedContent-338393Enabled", "SubscribedContent-353694Enabled",
                "SubscribedContent-353696Enabled", "SubscribedContent-353698Enabled"
            };
            foreach (string k in keys) SetReg(HKCU, cdm, k, 0);
            SetReg(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);
            Ok("Consumer features and suggestions off");
        }

        private void HideRecommended()
        {
            SetReg(HKCU, Adv, "Start_TrackDocs", 0);
            SetReg(HKCU, Adv, "Start_TrackProgs", 0);
            SetReg(HKCU, Adv, "Start_IrisRecommendations", 0);
            SetReg(HKCU, Adv, "Start_AccountNotifications", 0);
            SetReg(HKCU, Adv, "Start_Layout", 1);   // more pins, fewer recommendations
            SetReg(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Explorer", "HideRecommendedSection", 1);
            const string pm = @"SOFTWARE\Microsoft\PolicyManager\current\device";
            SetReg(HKLM, pm + @"\Start", "HideRecommendedSection", 1);
            SetReg(HKLM, pm + @"\Education", "IsEducationEnvironment", 1);   // lets the policy apply on Pro
            Ok("Recommended section hidden");
        }

        private void StartPins()
        {
            string json = "{\"pinnedList\":[" +
                string.Join(",", o.Pins.Select(p => "{\"packagedAppId\":\"" + p + "\"}")) + "]}";
            const string pm = @"SOFTWARE\Microsoft\PolicyManager\current\device\Start";
            SetStr(HKLM, pm, "ConfigureStartPins", json);
            SetReg(HKLM, pm, "ConfigureStartPins_ProviderSet", 1);

            string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(local))
            {
                string state = Path.Combine(local, @"Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState");
                if (Directory.Exists(state))
                    foreach (string f in Directory.GetFiles(state, "start*.bin"))
                    {
                        try { File.Delete(f); } catch { }
                    }
            }
            Ok("Start menu pins set (" + o.Pins.Count + " app(s))");
        }

        private void Telemetry()
        {
            const string dc = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
            SetReg(HKLM, dc, "AllowTelemetry", 0);
            SetReg(HKLM, dc, "DoNotShowFeedbackNotifications", 1);
            SetReg(HKCU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0);
            SetReg(HKCU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
            foreach (string svc in new[] { "DiagTrack", "dmwappushservice" })
            {
                Run("sc.exe", "stop " + svc);
                Run("sc.exe", "config " + svc + " start= disabled");
            }
            string[] tasks =
            {
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
            };
            foreach (string t in tasks) Run("schtasks.exe", "/Change /TN \"" + t + "\" /Disable");
            Ok("Telemetry off (Home/Pro still send a minimal required level - a Windows limit)");
        }

        private void Location()
        {
            const string loc = @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";
            SetReg(HKLM, loc, "DisableLocation", 1);
            SetReg(HKLM, loc, "DisableLocationScripting", 1);
            SetReg(HKLM, loc, "DisableWindowsLocationProvider", 1);
            const string consent = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location";
            SetStr(HKLM, consent, "Value", "Deny");
            SetStr(HKCU, consent, "Value", "Deny");
            Run("sc.exe", "stop lfsvc");
            Run("sc.exe", "config lfsvc start= disabled");
            Ok("Location services off");
        }

        private void BitLocker()
        {
            SetReg(HKLM, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1);
            PS("try { Get-BitLockerVolume -ErrorAction Stop | Where-Object { $_.VolumeStatus -ne 'FullyDecrypted' } | ForEach-Object { Write-Host \"Decrypting $($_.MountPoint) (continues in the background)...\"; Disable-BitLocker -MountPoint $_.MountPoint -ErrorAction SilentlyContinue | Out-Null } } catch { Write-Host '[!!] BitLocker module unavailable on this edition - skipped volume decryption' }");
            Ok("BitLocker handled");
        }

        private void PowerPlan()
        {
            const string guidPattern = @"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}";
            string output;
            string guid = null;

            Exec("powercfg", "/list", false, out output);
            Match existing = Regex.Match(output, "(" + guidPattern + @")\s+\(Ultimate Performance\)", RegexOptions.IgnoreCase);
            if (existing.Success)
            {
                guid = existing.Groups[1].Value;
            }
            else
            {
                Exec("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", false, out output);
                Match made = Regex.Match(output, guidPattern);
                if (made.Success) guid = made.Value;
            }

            if (guid != null)
            {
                Run("powercfg", "-setactive " + guid);
                Ok("Ultimate Performance is active");
            }
            else
            {
                Warn("This PC does not expose the Ultimate Performance plan (common on some laptops)");
            }
        }

        private void RestartExplorer()
        {
            Run("rundll32.exe", "user32.dll,UpdatePerUserSystemParameters 1, True");
            foreach (Process p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); } catch { }
            }
            Ok("Explorer restarted");
        }

        private void Browser(bool keepEdge)
        {
            bool ok = InstallOrUpdate(o.BrowserId, o.BrowserName, o.Has("browserupdate"));
            if (keepEdge)
            {
                if (ok) Ok(o.Has("browserupdate") ? "Edge is up to date" : "Edge left as it is");
                return;
            }
            if (!ok)
            {
                Warn("Could not install " + o.BrowserName + (o.Has("removeedge") ? ", so Edge was NOT removed (you would have no browser)." : "."));
                return;
            }
            Ok(o.BrowserName + (o.Has("browserupdate") ? " is installed and up to date" : " is installed"));
            if (o.Has("removeedge")) RemoveEdge();
        }

        private void RemoveEdge()
        {
            Info("Removing Microsoft Edge (the WebView2 runtime is kept - other apps need it)...");
            foreach (Process p in Process.GetProcessesByName("msedge"))
            {
                try { p.Kill(); } catch { }
            }
            SetStr(HKLM, @"SOFTWARE\Microsoft\EdgeUpdateDev", "AllowUninstall", "");

            string appDir = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)",
                                         @"Microsoft\Edge\Application");
            string setup = null;
            if (Directory.Exists(appDir))
            {
                var rx = new Regex(@"^\d+\.\d+\.\d+\.\d+$");
                setup = Directory.GetDirectories(appDir)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => rx.IsMatch(d.Name))
                    .OrderByDescending(d => new Version(d.Name))
                    .Select(d => Path.Combine(d.FullName, @"Installer\setup.exe"))
                    .FirstOrDefault(File.Exists);
            }
            if (setup == null)
            {
                Warn("Edge installer not found - already removed?");
                return;
            }

            string ignored;
            Exec(setup, "--uninstall --system-level --verbose-logging --force-uninstall", false, out ignored);

            string pub = Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
            string pd = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
            string[] leftovers =
            {
                Path.Combine(pub, @"Desktop\Microsoft Edge.lnk"),
                Path.Combine(pd, @"Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Microsoft Edge.lnk")
            };
            foreach (string f in leftovers)
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }

            SetReg(HKLM, @"SOFTWARE\Microsoft\EdgeUpdate", "DoNotUpdateToEdgeWithChromium", 1);   // stop it coming back

            if (File.Exists(Path.Combine(appDir, "msedge.exe")))
                Warn("Edge is still present (Windows blocked the uninstall on this build).");
            else
                Ok("Edge removed");
        }
    }
}
