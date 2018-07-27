namespace VSCodeHtmlCopy.Arguments

open Argu

[<CliPrefix(CliPrefix.DoubleDash)>]
[<NoAppSettings>]
type Arguments =
    | [<AltCommandLine("-c")>] Class of ClassName:string
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Class _ -> "Optionally remove the root style and add this provided class name."
