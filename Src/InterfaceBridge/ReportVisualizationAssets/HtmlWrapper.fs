namespace InterfaceBridge.ReportVisualizationAssets

open InterfaceBridge.ReportVisualizationAssets.Css
open System

        
type HtmlHead =
    internal 
        { charSet: string
          title: string
          baseCss: CssDeclaration list
          specificCss: CssDeclaration list
          script: string }

module HtmlHead =
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
    | Footer of string
    | H1 of string
    | H2 of string
    | H3 of string
    | Div
    | NestedSpan
    | Span of string
    | Bold of string
    | Paragraph
    | Table
    | TableRow
    | TableHeadCell of string
    | TableDataCell of string
    | NoTag of string

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

type TagType =
    | WrapperTag of string
    | ContentTag of string

module DomElement =
    let private createTagString tag identifier tagType =
        match tagType with
        | WrapperTag elements -> $"<{tag} {identifier} >{elements}</{tag}>"
        | ContentTag s -> $"<{tag} {identifier} >{s}</{tag}>"
        
    let rec internal toString e =
        let sortedSubElements =
            e.contents
            |> List.sortBy(_.ordinal)
            |> List.map(toString)
            |> String.concat Environment.NewLine
        let identifier =
            match e.identifierType with
            | Class c -> $"class=\"{c}\""
            | Id id -> $"id=\"{id}\""
            | NoIdentifier -> ""
        match e.elementType with
        | H1 s -> createTagString "h1" identifier (ContentTag s)
        | Span s -> createTagString "span" identifier (ContentTag s)
        | Bold s -> createTagString "b" identifier (ContentTag s)
        | Footer s -> createTagString "footer" identifier (ContentTag s)
        | NoTag s -> $" {s} "
        | Section -> createTagString "h1" identifier (WrapperTag sortedSubElements)
        | Header -> createTagString "header" identifier (WrapperTag sortedSubElements)
        | Div -> createTagString "div" identifier (WrapperTag sortedSubElements)
        | Paragraph -> createTagString "p" identifier (WrapperTag sortedSubElements)
        | NestedSpan -> createTagString "span" identifier (WrapperTag sortedSubElements)
        | _ -> "tag not implemented"
        
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
          head: HtmlHead
          body: HtmlBody }

module HtmlWrapper =
    let toString h = $"""
<!doctype html>
<html lang="{h.language}">
{h.head |> HtmlHead.toString}
{h.body |> HtmlBody.toString}
</html>
""" 
