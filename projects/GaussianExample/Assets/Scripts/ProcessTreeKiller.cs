using System.Diagnostics;

namespace SplattingPipeline
{
    public static class ProcessTreeKiller
    {
     
        public static void KillTree(int pid)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // taskkill's /T recurses through the tree; /F forces termination.
            RunFireAndForget("taskkill", $"/PID {pid} /T /F");
#else
            // pkill -P finds direct children of pid; recurse one level at a time
            // since pkill doesn't recurse grandchildren on its own.
            KillChildrenRecursive(pid);
            RunFireAndForget("kill", $"-9 {pid}");
#endif
        }

#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        private static void KillChildrenRecursive(int pid)
        {
            // "pgrep -P <pid>" lists direct child PIDs of pid.
            var psi = new ProcessStartInfo("pgrep", $"-P {pid}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var pgrep = Process.Start(psi);
            string output = pgrep.StandardOutput.ReadToEnd();
            pgrep.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                if (int.TryParse(line.Trim(), out int childPid))
                {
                    KillChildrenRecursive(childPid); // depth-first: kill grandchildren first
                    RunFireAndForget("kill", $"-9 {childPid}");
                }
            }
        }
#endif

        private static void RunFireAndForget(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
            }
            catch { /* best-effort cleanup, nothing more to do if this fails */ }
        }
    }
}
