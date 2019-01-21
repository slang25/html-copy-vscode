// Origional work Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Modified work Copyright (c) 2018 Stuart Lang <stuart.b.lang@gmail.com>.
// Licensed under the MIT Licence
module HtmlCopyVSCode.Clipboard

open System
open System.Runtime.InteropServices
open System.Text

type IClipboard =
    abstract member GetHtml : unit -> string
    abstract member SetText : string -> unit

[<Sealed>]
type internal WindowsClipboard() =
    [<DllImport("User32.dll", SetLastError = true)>]
    static extern bool IsClipboardFormatAvailable(uint32 format)

    [<DllImport("user32.dll", SetLastError = true)>]
    static extern uint32 RegisterClipboardFormatA(string lpszFormat)
    
    [<DllImport("User32.dll", SetLastError = true)>]
    static extern nativeint GetClipboardData(uint32 uFormat)

    [<DllImport("user32.dll", SetLastError = true)>]
    static extern nativeint SetClipboardData(uint32 uFormat, nativeint hMem)

    [<DllImport("User32.dll", SetLastError = true)>]
    static extern bool OpenClipboard(nativeint hWndNewOwner)

    [<DllImport("user32.dll", SetLastError = true)>]
    static extern bool EmptyClipboard()

    [<DllImport("User32.dll", SetLastError = true)>]
    static extern bool CloseClipboard()

    [<DllImport("Kernel32.dll", SetLastError = true)>]
    static extern nativeint GlobalLock(nativeint hMem)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    static extern nativeint GlobalAlloc(uint32 uFlags, unativeint dwBytes)

    [<DllImport("Kernel32.dll", SetLastError = true)>]
    static extern bool GlobalUnlock(nativeint hMem)

    [<DllImport("Kernel32.dll", SetLastError = true)>]
    static extern int GlobalSize(nativeint hMem)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    static extern nativeint GlobalFree(nativeint hMem)

    [<Literal>]
    let cf_unicodetext = 13u

    interface IClipboard with
        member __.GetHtml() =
            let cf_html = RegisterClipboardFormatA("HTML Format")

            if (not <| IsClipboardFormatAvailable(cf_html)) then
                null
            else
                try
                    if ( not <| OpenClipboard(IntPtr.Zero)) then
                        null
                    else
                        let handle = GetClipboardData(cf_html)
                        if handle = IntPtr.Zero then
                            null
                        else
                            let mutable pointer = IntPtr.Zero

                            try
                                pointer <- GlobalLock(handle)
                                if pointer = IntPtr.Zero then
                                    null
                                else
                                    let size = GlobalSize(handle)
                                    let buff : byte [] = Array.zeroCreate size

                                    Marshal.Copy(pointer, buff, 0, size)

                                    Encoding.UTF8.GetString(buff).TrimEnd('\000')
                            finally
                                if pointer <> IntPtr.Zero then
                                    GlobalUnlock(handle) |> ignore
                finally
                    CloseClipboard() |> ignore

        member __.SetText (text: string) =
            try
                if (not <| OpenClipboard(IntPtr.Zero)) then
                    ()
                else
                    EmptyClipboard() |> ignore

                    let bytes = (text.Length + 1) * 2 |> uint32

                    let source = Marshal.StringToHGlobalUni(text)

                    let gmem_movable : int = 0x0002
                    let gmem_zeroinit : int = 0x0040
                    let ghnd = gmem_movable ||| gmem_zeroinit |> uint32

                    // IMPORTANT: SetClipboardData requires memory that was acquired with GlobalAlloc using GMEM_MOVABLE.
                    let mutable hGlobal = GlobalAlloc(ghnd, bytes |> unativeint)

                    try
                        let target = GlobalLock(hGlobal)
                        if target = IntPtr.Zero then
                           ()
                        else
                            try
                                Buffer.MemoryCopy(source.ToPointer(), target.ToPointer(), bytes |> int64, bytes |> int64)
                            finally
                                if target <> IntPtr.Zero then
                                    GlobalUnlock(target) |> ignore
                                Marshal.FreeHGlobal(source)

                            if SetClipboardData(cf_unicodetext, hGlobal).ToInt64() <> 0L then
                                // IMPORTANT: SetClipboardData takes ownership of hGlobal upon success.
                                hGlobal <- IntPtr.Zero
                    finally
                        if hGlobal <> IntPtr.Zero then
                            GlobalFree(hGlobal) |> ignore
            finally
                CloseClipboard() |> ignore

[<Sealed>]
type internal MacOSClipboard() =
    [<DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")>]
    static extern nativeint objc_getClass(string className)

    [<DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", EntryPoint="objc_msgSend")>]
    static extern nativeint objc_msgSend1(nativeint receiver, nativeint selector)

    [<DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", EntryPoint="objc_msgSend")>]
    static extern nativeint objc_msgSend2(nativeint receiver, nativeint selector, string arg1)

    [<DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", EntryPoint="objc_msgSend")>]
    static extern nativeint objc_msgSend3(nativeint receiver, nativeint selector, nativeint arg1)

    [<DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", EntryPoint="objc_msgSend")>]
    static extern nativeint objc_msgSend4(nativeint receiver, nativeint selector, nativeint arg1, nativeint arg2)

    [<DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")>]
    static extern nativeint sel_registerName(string selectorName)
    
    let nsString = objc_getClass "NSString"
    let nsPasteboard = objc_getClass "NSPasteboard"
    let utfTextType = objc_msgSend2(objc_msgSend1(nsString, sel_registerName "alloc"), sel_registerName "initWithUTF8String:", "public.utf8-plain-text")
    let htmlType = objc_msgSend2(objc_msgSend1(nsString, sel_registerName "alloc"), sel_registerName "initWithUTF8String:", "public.html")

    let generalPasteboard = objc_msgSend1(nsPasteboard, sel_registerName "generalPasteboard")
    
    interface IClipboard with
        member __.GetHtml() =
            let ptr = objc_msgSend3(generalPasteboard, sel_registerName "stringForType:", htmlType)
            let charArray = objc_msgSend1(ptr, sel_registerName "UTF8String")
            Marshal.PtrToStringAnsi(charArray)

        member __.SetText(text:string) =
            let mutable str = IntPtr.Zero;
            try
                str <- objc_msgSend2(objc_msgSend1(nsString, sel_registerName "alloc"), sel_registerName "initWithUTF8String:", text)
                objc_msgSend1(generalPasteboard, sel_registerName "clearContents") |> ignore
                objc_msgSend4(generalPasteboard, sel_registerName "setString:forType:", str, utfTextType) |> ignore
            finally
                if str <> IntPtr.Zero then objc_msgSend1(str, sel_registerName "release") |> ignore

let create() =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows
            then WindowsClipboard() :> IClipboard
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX
            then MacOSClipboard() :> IClipboard
        else raise <| PlatformNotSupportedException "Your platform isn't yet supported. macOS support is coming soon."
