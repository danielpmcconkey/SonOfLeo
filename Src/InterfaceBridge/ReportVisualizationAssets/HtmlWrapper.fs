namespace InterfaceBridge.ReportVisualizationAssets

type CssDeclaration =
    internal
        { ordinal: int
          declarator: string
          definition: string }
        
type HtmlHeader =
    internal 
        { charSet: string
          title: string
          baseCss: CssDeclaration list
          specificCss: CssDeclaration list
          script: string }

type DomElementType =
    | Section
    | Header
    | H1 of string
    | H2 of string
    | H3 of string
    | Div
    | Span of string
    | Paragraph of string
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
        
type HtmlBody =
    internal 
        { header: HtmlHeader
          body: DomElement list }
        
type HtmlWrapper =
    internal
        { language: string
          header: HtmlHeader
          body: HtmlBody }
    
