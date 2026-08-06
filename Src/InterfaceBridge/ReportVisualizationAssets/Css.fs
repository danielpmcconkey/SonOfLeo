module InterfaceBridge.ReportVisualizationAssets.Css

type CssDeclaration =
    internal
        { ordinal: int
          declarator: string
          definition: string }

module CssDeclaration =
    let toString c = $"{c.declarator} {{{c.definition}}}"
        

let baseCssDeclarations = [
    {
        ordinal = 10
        declarator = ":root"
        definition = """
    --ink: #1a1a1a;
    --ink-muted: #666;
    --ink-light: #9a968c;
    --rule: #e5e2d8;
    --rule-strong: #b8b3a4;
    --bg: #f3f1ea;
    --panel: #fffdf8;
    --neg: #8a1f1c;
    --zero: #b0ada5;""" }
    {
        ordinal = 20
        declarator = "*"
        definition = "box-sizing: border-box;" }
    {
        ordinal = 30
        declarator = "html, body"
        definition = "box-sizing: border-box;" }
    {
        ordinal = 40
        declarator = "body"
        definition = """
    font-family: "Helvetica Neue", "Segoe UI", system-ui, -apple-system, sans-serif;
    font-size: 14px;
    line-height: 1.45;
    -webkit-font-smoothing: antialiased;""" }
    {
        ordinal = 50
        declarator = ".report"
        definition = """
    max-width: 1120px;
    margin: 3rem auto;
    padding: 3rem 3.5rem 3.5rem;
    background: var(--panel);
    box-shadow: 0 2px 6px rgba(0,0,0,0.04), 0 0 0 1px var(--rule);""" }
    {
        ordinal = 60
        declarator = ".report-head"
        definition = """
    border-bottom: 2px solid var(--ink);
    padding-bottom: 1.25rem;
    margin-bottom: 2.5rem;""" }
    {
        ordinal = 70
        declarator = ".report-head h1"
        definition = """
    font-size: 1.55rem;
    font-weight: 500;
    margin: 0 0 0.4rem;
    letter-spacing: -0.01em;""" }
    {
        ordinal = 80
        declarator = ".report-head .range"
        definition = "color: var(--ink-muted); font-size: 0.9rem;" }
    {
        ordinal = 90
        declarator = ".report-head .range b"
        definition = """
    margin-top: 3rem;
    padding-top: 1rem;
    border-top: 1px solid var(--rule);
    color: var(--ink-light);
    font-size: 0.75rem;
    font-style: italic;""" }
]
