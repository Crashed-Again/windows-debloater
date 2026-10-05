using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Cub
{
    internal sealed class WingetHit
    {
        public string Name;
        public string Id;
        public string Version;
    }

    internal static class WingetSearch
    {
        /// <summary>Runs "winget search" and parses its table output. Returns null and sets error on failure.</summary>
        public static List<WingetHit> Run(string query, out string error)
        {
            error = null;
            var hits = new List<WingetHit>();
            try
            {
                string q = query.Replace("\"", "").Trim();
                if (q.Length == 0) { error = "Type something to search for."; return null; }

                var psi = new ProcessStartInfo("winget", "search \"" + q + "\" --accept-source-agreements --disable-interactivity")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                string output;
                using (Process p = Process.Start(psi))
                {
                    var sb = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) { sb.AppendLine(e.Data); } };
                    p.ErrorDataReceived += (s, e) => { };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(60000))
                    {
                        try { p.Kill(); } catch { }
                        error = "winget took too long to answer.";
                        return null;
                    }
                    p.WaitForExit();
                    output = sb.ToString();
                }

                string[] lines = output.Split('\n');
                int headerIndex = -1;
                int iName = 0, iId = 0, iVer = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = Clean(lines[i]);
                    if (line.StartsWith("Name") && line.Contains("Id") && line.Contains("Version"))
                    {
                        headerIndex = i;
                        iName = line.IndexOf("Name", StringComparison.Ordinal);
                        iId = line.IndexOf("Id", StringComparison.Ordinal);
                        iVer = line.IndexOf("Version", StringComparison.Ordinal);
                        break;
                    }
                }
                if (headerIndex < 0)
                {
                    error = "No results.";
                    return hits;
                }

                for (int i = headerIndex + 2; i < lines.Length && hits.Count < 60; i++)   // +2 skips the dashed line
                {
                    string line = Clean(lines[i]);
                    if (line.Length <= iVer) continue;
                    string name = line.Substring(iName, Math.Max(0, iId - iName)).Trim();
                    string id = line.Substring(iId, Math.Max(0, iVer - iId)).Trim();
                    string rest = line.Substring(iVer).Trim();
                    string ver = rest.Split(' ')[0];
                    if (id.Length == 0 || id.IndexOf(' ') >= 0) continue;
                    hits.Add(new WingetHit { Name = name, Id = id, Version = ver });
                }
                if (hits.Count == 0) error = "No results.";
                return hits;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                error = "winget was not found. Install or update \"App Installer\" from the Microsoft Store.";
                return null;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        // winget redraws a spinner with carriage returns - keep only what follows the last one
        private static string Clean(string line)
        {
            line = line.TrimEnd('\r');
            int cr = line.LastIndexOf('\r');
            return cr >= 0 ? line.Substring(cr + 1) : line;
        }
    }
}
