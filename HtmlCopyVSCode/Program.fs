module HtmlCopyVSCode.Main

open Argu
open AngleSharp.Dom
open AngleSharp.Html
open AngleSharp.Parser.Html
open AngleSharp.XHtml
open System
open System.IO
open System.Linq
open System.Runtime.InteropServices
open System.Text
open HtmlCopyVSCode.Arguments

let [<Literal>] private vscodePrefix = """<meta charset='utf-8'>"""
let [<Literal>] private riderPrefix = """<html><head><meta http-equiv="content-type" content="text/html; charset=UTF-8"></head><body>"""
let [<Literal>] private riderSuffix = """</body></html>"""

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<Arguments>(programName = "html-copy-vscode", errorHandler = errorHandler)

    let results = (parser.ParseCommandLine argv).GetAllResults()
    let className = match results with
                        | (Class className : Arguments) :: _ -> Some className
                        | _ -> None
    
    let clipboard = Clipboard.create()
    
    let origionalFragment = 
        let content = clipboard.GetHtml()
        
        let expect condition =
                        if not condition then
                            printf "Unexpected clipboard contents"
                            exit 1
        
        if RuntimeInformation.IsOSPlatform OSPlatform.OSX then
            printf "%s" content
            if content.StartsWith vscodePrefix then
                content.[vscodePrefix.Length ..]
            elif content.StartsWith riderPrefix then
                content.[riderPrefix.Length .. (content.Length-riderSuffix.Length - 1)]
            else
                printf "Unexpected clipboard contents"
                exit 1
            
        elif RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            let isNotNull = isNull >> not
    
            expect(content |> isNotNull)
    
            use reader = new StringReader(content)
            
            let versionLine = reader.ReadLine()
    
            expect(versionLine = "Version:0.9")
    
            let skipLines numberOfLines (reader: StringReader) =
                for _ in 1 .. numberOfLines do
                    reader.ReadLine() |> ignore
    
            reader |> skipLines 2
            
            let startFragmentLine = reader.ReadLine()
            let startFragment = startFragmentLine.Substring("StartFragment:".Length) |> int
            
            let endFragmentLine = reader.ReadLine()
            let endFragment = endFragmentLine.Substring("EndFragment:".Length) |> int
            
            let bytes = Encoding.UTF8.GetBytes(content)
            Encoding.UTF8.GetString(bytes, startFragment, endFragment - startFragment)
        else failwith "Unsupported platform"

    let fragment =
        match className with
        | Some className ->
            let nodes = 
                let p = new HtmlParser()
                let dom = p.Parse "<html><body></body></html>"
                p.ParseFragment(origionalFragment, dom.Body)

            let rootDiv = nodes.First() :?> IElement
            rootDiv.ClassName <- className
            
            // keep some style so that markdown previews still look alright
            rootDiv.RemoveAttribute "style" |> ignore
            rootDiv.SetAttribute("style", "white-space: pre;")

            use sw = new StringWriter()

            let rec getAllElements predicate (parent: INode) =
                seq {
                    for i in 0 .. parent.ChildNodes.Length - 1 do
                        match parent.ChildNodes.[i] with
                        | :? IElement as child -> 
                            if predicate child then yield child
                            yield! parent.ChildNodes.[i] |> getAllElements predicate
                        | _ -> ()
                }

            // Optimization: Remove color style on whitespace spans
            let isWhitespaceSpan (e: IElement) = e.TagName = TagNames.Span && String.IsNullOrWhiteSpace(e.TextContent)
            let removeStyle (node: IElement) = node.RemoveAttribute "style" |> ignore
            nodes |> Seq.collect (getAllElements isWhitespaceSpan)
                  |> Seq.iter removeStyle
            
            nodes.ToHtml(sw, XhtmlMarkupFormatter.Instance)
            sw.ToString()
        | None -> origionalFragment
    
    clipboard.SetText fragment
    0
