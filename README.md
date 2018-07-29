# html-copy-vscode
A global tool to convert snippets copied from VS Code into plain html to paste into your blog.

[![NuGet](https://img.shields.io/nuget/v/HtmlCopyVSCode.svg)](https://www.nuget.org/packages/HtmlCopyVSCode/) [![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) 

## Installation
```
dotnet tool install -g HtmlCopyVSCode
```

## Usage
In VS Code, copy some code to the clipboard with Ctrl+C (or Cmd+C on macOS), then run the tool:
```
html-copy-vscode
```
Now your clipboard contains the html snippet in plain text.

To remove most of the style on the root element and to add your own class, use the --class/-c switch like this:
```
html-copy-vscode -c vscode
```

## Know Issues
At the moment, only Windows is supported. macOS support is possible, but will take time (unless someone knows a quick way to aquire and distribute Xamarin.Mac which looks like the easiest option).
