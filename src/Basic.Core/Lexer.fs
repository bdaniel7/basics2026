namespace Basic.Core

open System

module Lexer =

    type Token =
        | NumLit of float
        | StrLit of string
        | Ident of string
        | Plus
        | Minus
        | Star
        | Slash
        | Caret
        | Eq
        | Ne
        | Lt
        | Le
        | Gt
        | Ge
        | LParen
        | RParen
        | Comma
        | Semi
        | Colon
        | RemLit of string
        | NewLine
        | Eof

    exception BasicLexError of string

    let private isIdentStart c =
        Char.IsLetter c || c = '_'

    let private isIdentChar c =
        Char.IsLetterOrDigit c || c = '_' || c = '$'

    let tokenize (source: string) : Token list =
        let tokens = ResizeArray<Token>()
        let len = source.Length
        let mutable i = 0

        let fail msg = raise (BasicLexError msg)

        while i < len do
            let c = source.[i]

            if c = ' ' || c = '\t' || c = '\r' then
                i <- i + 1
            elif c = '\n' then
                tokens.Add NewLine
                i <- i + 1
            elif Char.IsDigit c || (c = '.' && i + 1 < len && Char.IsDigit source.[i + 1]) then
                let start = i

                while i < len && (Char.IsDigit source.[i] || source.[i] = '.') do
                    i <- i + 1

                let text = source.Substring(start, i - start)
                tokens.Add(NumLit(Double.Parse(text, Globalization.CultureInfo.InvariantCulture)))
            elif isIdentStart c then
                let start = i

                while i < len && isIdentChar source.[i] do
                    i <- i + 1

                let text = source.Substring(start, i - start)

                if text.ToUpperInvariant() = "REM" then
                    let cstart = i

                    while i < len && source.[i] <> '\n' do
                        i <- i + 1

                    tokens.Add(RemLit(source.Substring(cstart, i - cstart).Trim()))
                else
                    tokens.Add(Ident text)
            elif c = '"' then
                i <- i + 1
                let start = i

                while i < len && source.[i] <> '"' && source.[i] <> '\n' do
                    i <- i + 1

                if i >= len || source.[i] <> '"' then
                    fail "Unterminated string literal"

                tokens.Add(StrLit(source.Substring(start, i - start)))
                i <- i + 1
            else
                let next =
                    if i + 1 < len then Some source.[i + 1] else None

                match c, next with
                | '+', _ ->
                    tokens.Add Plus
                    i <- i + 1
                | '-', _ ->
                    tokens.Add Minus
                    i <- i + 1
                | '*', _ ->
                    tokens.Add Star
                    i <- i + 1
                | '/', _ ->
                    tokens.Add Slash
                    i <- i + 1
                | '^', _ ->
                    tokens.Add Caret
                    i <- i + 1
                | '=', _ ->
                    tokens.Add Eq
                    i <- i + 1
                | '<', Some '>' ->
                    tokens.Add Ne
                    i <- i + 2
                | '<', Some '=' ->
                    tokens.Add Le
                    i <- i + 2
                | '<', _ ->
                    tokens.Add Lt
                    i <- i + 1
                | '>', Some '=' ->
                    tokens.Add Ge
                    i <- i + 2
                | '>', _ ->
                    tokens.Add Gt
                    i <- i + 1
                | '(', _ ->
                    tokens.Add LParen
                    i <- i + 1
                | ')', _ ->
                    tokens.Add RParen
                    i <- i + 1
                | ',', _ ->
                    tokens.Add Comma
                    i <- i + 1
                | ';', _ ->
                    tokens.Add Semi
                    i <- i + 1
                | ':', _ ->
                    tokens.Add Colon
                    i <- i + 1
                | _ -> fail (sprintf "Unexpected character '%c'" c)

        tokens.Add Eof
        List.ofSeq tokens
