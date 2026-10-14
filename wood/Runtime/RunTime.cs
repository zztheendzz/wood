using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace wood.Runtime
{
    public static class RunTime
    {
        [ModuleInitializer]
        public static void EnsureHalconRuntime()
        {
            // Thư mục thật chứa exe (đúng cả khi publish single-file)
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

            // Thư mục halcon-runtime nằm cạnh exe
            var halconRoot = Path.Combine(exeDir, "halcon-runtime");
            var binDir = Path.Combine(halconRoot, "bin", "x64-win64");
            var genicamDir = Path.Combine(halconRoot, "genicam", "bin", "Win64_x64");

            Environment.SetEnvironmentVariable("HALCONROOT", halconRoot);
            Environment.SetEnvironmentVariable("HALCONARCH", "x64-win64");

            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            var extra = binDir + Path.PathSeparator + genicamDir + Path.PathSeparator;
            if (!path.Contains(binDir, StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PATH", extra + path);
        }
    }
}