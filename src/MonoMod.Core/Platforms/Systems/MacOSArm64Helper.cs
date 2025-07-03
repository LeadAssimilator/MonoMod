using Microsoft.Win32.SafeHandles;
using MonoMod.Core.Interop;
using MonoMod.Utils;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace MonoMod.Core.Platforms.Systems
{
    /// <summary>
    /// Native helper for macOS arm64
    /// </summary>
    public class MacOSArm64Helper
    {
        /// <summary>
        /// Singleton instance created by a call to <see cref="Initialize"/>.
        /// </summary>
        public static MacOSArm64Helper? Instance { get; private set; }

        private const string LogicalName = "helper_macos_arm64.dylib";
        private const string TempFileNameTmpl = "/tmp/mm-macos-silicon-helper.dylib.XXXXXX";
        private const string JitMemCpyExport = "mmch_jit_memcpy";
        private const string JitHookConfigExport = "mmch_jit_hook_config";

        private readonly IntPtr _handle;
        private readonly IntPtr _jitMemCpy;
        private readonly IntPtr _jitHookConfig;

        private MacOSArm64Helper(string fileName)
        {
            _handle = DynDll.OpenLibrary(fileName);

            try
            {
                _jitMemCpy = DynDll.GetExport(_handle, JitMemCpyExport);
                Helpers.Assert(_jitMemCpy != IntPtr.Zero);

                _jitHookConfig = DynDll.GetExport(_handle, JitHookConfigExport);
                Helpers.Assert(_jitHookConfig != IntPtr.Zero);
            }
            catch
            {
                DynDll.CloseLibrary(_handle);
                throw;
            }
        }

        /// <summary>
        /// JIT_MAP (RWX) safe memory copy
        /// </summary>
        /// <param name="dst">Destination</param>
        /// <param name="src">Source</param>
        /// <param name="size">Size of memory that should be copied from dst to src</param>
        public unsafe void JitMemCpy(IntPtr dst, IntPtr src, ulong size)
        {
            var fnPtr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, ulong, void>)_jitMemCpy;

            fnPtr(dst, src, size);
        }

        /// <summary>
        /// Gets the pointer to the native jit hook configuration struct which can vary by both runtime and arch.
        /// </summary>
        /// <param name="runtimeMajMin">Runtime major and minor version.</param>
        /// <returns>A pointer to the requested jit hook configuration struct.</returns>
        internal unsafe IntPtr GetJitHookConfig(int runtimeMajMin)
        {
            var fnPtr = (delegate* unmanaged[Cdecl]<int, IntPtr>)_jitHookConfig;
            return fnPtr(runtimeMajMin);
        }

        private static void CreateTempFile(string tmpl, out int fd, out string fileName)
        {
            var tmplName = ArrayPool<byte>.Shared.Rent(tmpl.Length + 1);

            try
            {
                unsafe
                {
                    tmplName.AsSpan().Clear();
                    Encoding.UTF8.GetBytes(tmpl).AsSpan().CopyTo(tmplName);

                    fixed (byte* tmplNamePtr = tmplName)
                        fd = OSX.MkSTemp(tmplNamePtr);

                    if (fd == -1)
                    {
                        var lastError = OSX.Errno;
                        var ex = new Win32Exception(lastError);
                        MMDbgLog.Error($"Could not create temp file for {nameof(MacOSArm64Helper)}: {lastError} {ex}");
                        throw ex;
                    }

                    fileName = Encoding.UTF8.GetString(tmplName, 0, tmpl.Length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tmplName);
            }
        }

        private static void ExtractEmbeddedResourcesToTempFile(int fd, string logicalName)
        {
            using (var handle = new SafeFileHandle((IntPtr)fd, true))
            using (var stream = new FileStream(handle, FileAccess.Write))
            {
                using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
                Helpers.Assert(embedded is not null);

                embedded.CopyTo(stream);
            }
        }

        private static string GetTempFileFromEmbeddedResources(string logicalName)
        {
            // Filestream with non-Mono internal created file descriptors do not work. See:
            // https://github.com/mono/mono/issues/12783
            if (PlatformDetection.Runtime is RuntimeKind.Mono)
            {
                var tempFilePath = $"{Path.GetTempPath()}{logicalName}.{AssemblyInfo.AssemblyVersion}";
                
                using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
                Helpers.Assert(embedded is not null);

                using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
                embedded.CopyTo(fileStream);
                
                return tempFilePath;
            }

            CreateTempFile(TempFileNameTmpl, out var fd, out var fileName);
            ExtractEmbeddedResourcesToTempFile(fd, logicalName);

            return fileName;
        }

        /// <summary>
        /// Creates Native Helper
        /// </summary>
        /// <returns>MacOSArm64Helper</returns>
        public static void Initialize()
        {
            Helpers.Assert(Instance is null);

            var fileName = File.Exists(LogicalName) ? LogicalName : GetTempFileFromEmbeddedResources(LogicalName);
            Instance = new MacOSArm64Helper(fileName);

            MMDbgLog.Trace($"{nameof(MacOSArm64Helper)} has been initialized.");
        }
    }
}
