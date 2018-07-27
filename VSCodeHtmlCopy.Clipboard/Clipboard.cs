// Origional work Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Modified work Copyright (c) 2018 Stuart Lang <stuart.b.lang@gmail.com>.
// Licensed under the MIT Licence

using System;
using System.Runtime.InteropServices;
using System.Text;


namespace VSCodeHtmlCopy.Clipboard
{
    public static class ClipboardFactory
    {
        public static IClipbloard Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsClipboard();
            }
            throw new PlatformNotSupportedException("Your platform isn't yet supported. macOS support is coming soon.");
        }
    }

    public interface IClipbloard
    {
        string GetHtml();
        void SetText(string text);
    }

    public class WindowsClipboard : IClipbloard
    {
        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint RegisterClipboardFormatA(string lpszFormat);
        
        [DllImport("User32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("Kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern int GlobalSize(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint cf_unicodetext = 13U;

        public string GetHtml()
        {
            uint cf_html = RegisterClipboardFormatA("HTML Format");

            if (!IsClipboardFormatAvailable(cf_html))
                return null;

            try
            {
                if (!OpenClipboard(default))
                    return null;

                IntPtr handle = GetClipboardData(cf_html);
                if (handle == default)
                    return null;

                IntPtr pointer = default;

                try
                {
                    pointer = GlobalLock(handle);

                    if (pointer == default)
                        return null;

                    int size = GlobalSize(handle);
                    byte[] buff = new byte[size];

                    Marshal.Copy(pointer, buff, 0, size);

                    return Encoding.UTF8.GetString(buff).TrimEnd('\0');
                }
                finally
                {
                    if (pointer != default)
                        GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        public void SetText(string text)
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                    return;

                EmptyClipboard();

                uint bytes = ((uint)text.Length + 1) * 2;

                var source = Marshal.StringToHGlobalUni(text);

                const int gmem_movable = 0x0002;
                const int gmem_zeroinit = 0x0040;
                const int ghnd = gmem_movable | gmem_zeroinit;

                // IMPORTANT: SetClipboardData requires memory that was acquired with GlobalAlloc using GMEM_MOVABLE.
                var hGlobal = GlobalAlloc(ghnd, (UIntPtr)bytes);

                try
                {
                    var target = GlobalLock(hGlobal);
                    if (target == default)
                        return;

                    try
                    {
                        unsafe
                        {
                            Buffer.MemoryCopy((void*)source, (void*)target, bytes, bytes);
                        }
                    }
                    finally
                    {
                        if (target != default)
                            GlobalUnlock(target);

                        Marshal.FreeHGlobal(source);
                    }

                    if (SetClipboardData(cf_unicodetext, hGlobal).ToInt64() != 0)
                    {
                        // IMPORTANT: SetClipboardData takes ownership of hGlobal upon success.
                        hGlobal = default;
                    }
                }
                finally
                {
                    if (hGlobal != default)
                        GlobalFree(hGlobal);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}