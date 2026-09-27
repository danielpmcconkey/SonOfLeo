module Tests.Helpers.LiteralSearch

(* REQ-SYS-1.4 cases. Each is search text carrying one character that is special in SQL LIKE, a value containing that
   text, and a decoy that lacks it but that the same search would match if the character were not escaped: % would
   match any run of characters, _ any one character, and a backslash would escape the character after it. *)

type Case = { search: string; containing: string; decoy: string }

let case (marker: string) (special: string) : Case =
    let unescapedMatch =
        match special with
        | "%" -> " then "
        | "_" -> "X"
        | @"\" -> ""
        | @"\." -> "." // a backslash that keeps a regular expression valid: \. is a literal dot there
        | other -> failwith $"no decoy is defined for {other}"
    let search = $"{marker}50{special}off"
    { search = search
      containing = $"has {search} inside"
      decoy = $"has {marker}50{unescapedMatch}off inside" }
