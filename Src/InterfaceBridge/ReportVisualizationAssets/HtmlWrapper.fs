namespace InterfaceBridge.ReportVisualizationAssets

open InterfaceBridge.ReportVisualizationAssets.Css
open System

        
type HtmlHeader =
    internal 
        { charSet: string
          title: string
          baseCss: CssDeclaration list
          specificCss: CssDeclaration list
          script: string }

module HtmlHeader =
    let toString h =
        let baseCss =
            h.baseCss
            |> List.sortBy(_.ordinal)
            |> List.map CssDeclaration.toString
            |> String.concat Environment.NewLine
        let specificCss =
            h.specificCss
            |> List.sortBy(_.ordinal)
            |> List.map CssDeclaration.toString
            |> String.concat Environment.NewLine
        $"""
    <head>
        <meta charset="{h.charSet}">
        <title>{h.title}</title>
        <style>
        {baseCss}
        {specificCss}

  /* ----- print ----- */
  @media print {{
    body {{ background: #fff; }}
    .report {{ box-shadow: none; margin: 0; padding: 1rem 1.5rem; max-width: none; }}
    .acct, .acct-label, .acct > table.tx {{ break-inside: avoid; }}
    .acct.level-0 {{ break-before: auto; }}
        </style>
        {h.script}
    </head>
"""

type DomElementType =
    | Section
    | Header
    | H1 of string
    | H2 of string
    | H3 of string
    | Div
    | Span of string
    | Paragraph
    | Table
    | TableRow
    | TableHeadCell of string
    | TableDataCell of string
    | None of string

type DomElementIdentifier =
    | Id of string
    | Class of string
    | NoIdentifier
    
type DomElement =
    internal
        { ordinal: int
          elementType: DomElementType
          identifierType: DomElementIdentifier
          contents: DomElement list }

module DomElement =
    let rec toString e =
        let sortedSubElements =
            e.contents
            |> List.sortBy(_.ordinal)
            |> List.map(toString)
            |> String.concat Environment.NewLine
        match e.elementType with
        | Span s -> $"<span>{s}</span>"
        | Paragraph -> $"<p>{sortedSubElements}</p>"
        | _ -> ""
        
type HtmlBody =
    internal 
        { elements: DomElement list }

module HtmlBody =
    let toString b =
        let elementsAsString =
            b.elements
            |> List.sortBy(_.ordinal)
            |> List.map DomElement.toString
            |> String.concat " "
        $"""
    <body>
        {elementsAsString}
    </body>
"""
        
type HtmlWrapper =
    internal
        { language: string
          header: HtmlHeader
          body: HtmlBody }

module HtmlWrapper =
    let toString h = $"""
<!doctype html>
<html lang="{h.language}">
{h.header |> HtmlHeader.toString}
{h.body |> HtmlBody.toString}
</html>
""" 
