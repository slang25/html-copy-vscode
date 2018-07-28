module HtmlCopyVSCode.Main

open Argu
open AngleSharp.Dom
open AngleSharp.Html
open AngleSharp.Parser.Html
open AngleSharp.XHtml
open System
open System.IO
open System.Linq
open System.Text
open HtmlCopyVSCode.Arguments
open HtmlCopyVSCode.Clipboard

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<Arguments>(programName = "html-copy-vscode", errorHandler = errorHandler)

    let results = (parser.ParseCommandLine argv).GetAllResults()
    let className = match results with
                    | (Class className : Arguments) :: _ -> Some className
                    | _ -> None
    
    let clipboard = Clipboard.create()

    let content = clipboard.GetHtml()
    
    let expect condition =
        if not condition then
            printf "Unexpected clipboard contents"
            exit 1

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
    let origionalFragment = Encoding.UTF8.GetString(bytes, startFragment, endFragment - startFragment)
    let fragment =
        match className with
        | Some className ->
            let p = new HtmlParser()
            let dom = p.Parse "<html><body></body></html>"
            let nodes = p.ParseFragment(origionalFragment, dom.Body)

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
            nodes |> Seq.collect (getAllElements(fun e -> e.TagName = TagNames.Span && String.IsNullOrWhiteSpace(e.TextContent)))
                  |> Seq.iter (fun node -> node.RemoveAttribute "style" |> ignore)
            
            nodes.ToHtml(sw, XhtmlMarkupFormatter.Instance)
            sw.ToString()
        | None -> origionalFragment
    
    clipboard.SetText fragment
    0
