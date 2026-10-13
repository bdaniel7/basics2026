namespace Basic.Core

open Basic.Core.Ast
open Basic.Core.Lexer

module Parser =

    exception BasicSyntaxError of string

    type private State =
        { Tokens: Token[]
          mutable Pos: int }

    let private peek (s: State) = s.Tokens.[s.Pos]

    let private peekAt (s: State) n =
        let idx = min (s.Pos + n) (s.Tokens.Length - 1)
        s.Tokens.[idx]

    let private advance (s: State) =
        let t = s.Tokens.[s.Pos]
        if s.Pos < s.Tokens.Length - 1 then
            s.Pos <- s.Pos + 1
        t

    let private fail msg = raise (BasicSyntaxError msg)

    let private expectToken (s: State) (t: Token) msg =
        if peek s = t then
            advance s |> ignore
        else
            fail msg

    let private asKeyword (t: Token) =
        match t with
        | Ident name -> Some(name.ToUpperInvariant())
        | _ -> None

    // --- Expressions -------------------------------------------------------

    let rec private parseExpr (s: State) : Expr = parseOr s

    and private parseOr (s: State) : Expr =
        let mutable left = parseAnd s

        while asKeyword (peek s) = Some "OR" do
            advance s |> ignore
            let right = parseAnd s
            left <- Binary(Or, left, right)

        left

    and private parseAnd (s: State) : Expr =
        let mutable left = parseComparison s

        while (match asKeyword (peek s) with
               | Some "AND" -> true
               | _ -> false) do
            advance s |> ignore
            let right = parseComparison s
            left <- Binary(And, left, right)

        left

    and private parseComparison (s: State) : Expr =
        let mutable left = parseAdditive s

        let notDone () =
            match peek s with
            | Eq | Ne | Lt | Le | Gt | Ge -> true
            | _ -> false

        while notDone () do
            let op =
                match advance s with
                | Eq -> Ast.Eq
                | Ne -> Ast.Ne
                | Lt -> Ast.Lt
                | Le -> Ast.Le
                | Gt -> Ast.Gt
                | Ge -> Ast.Ge
                | _ -> fail "Expected comparison operator"

            let right = parseAdditive s
            left <- Binary(op, left, right)

        left

    and private parseAdditive (s: State) : Expr =
        let mutable left = parseMultiplicative s

        let mutable go = true

        while go do
            match peek s with
            | Plus ->
                advance s |> ignore
                left <- Binary(Add, left, parseMultiplicative s)
            | Minus ->
                advance s |> ignore
                left <- Binary(Sub, left, parseMultiplicative s)
            | _ -> go <- false

        left

    and private parseMultiplicative (s: State) : Expr =
        let mutable left = parseUnary s

        let mutable go = true

        while go do
            match peek s with
            | Star ->
                advance s |> ignore
                left <- Binary(Mul, left, parseUnary s)
            | Slash ->
                advance s |> ignore
                left <- Binary(Div, left, parseUnary s)
            | Ident name when name.ToUpperInvariant() = "MOD" ->
                advance s |> ignore
                left <- Binary(Mod, left, parseUnary s)
            | _ -> go <- false

        left

    and private parseUnary (s: State) : Expr =
        match peek s with
        | Minus ->
            advance s |> ignore
            Unary(Neg, parseUnary s)
        | Plus ->
            advance s |> ignore
            parseUnary s
        | Ident name when name.ToUpperInvariant() = "NOT" ->
            advance s |> ignore
            Unary(Not, parseUnary s)
        | _ -> parsePower s

    and private parsePower (s: State) : Expr =
        let left = parsePrimary s

        match peek s with
        | Caret ->
            advance s |> ignore
            Call("POW", [ left; parseUnary s ])
        | _ -> left

    and private parsePrimary (s: State) : Expr =
        match peek s with
        | NumLit n ->
            advance s |> ignore
            Number n
        | StrLit str ->
            advance s |> ignore
            Str str
        | LParen ->
            advance s |> ignore
            let e = parseExpr s
            expectToken s RParen "Expected ')'"
            e
        | Ident name ->
            advance s |> ignore
            let upper = name.ToUpperInvariant()

            if peek s = LParen then
                advance s |> ignore

                let args = ResizeArray<Expr>()

                if peek s <> RParen then
                    args.Add(parseExpr s)

                    while peek s = Comma do
                        advance s |> ignore
                        args.Add(parseExpr s)

                expectToken s RParen "Expected ')'"
                Call(upper, List.ofSeq args)
            else
                Var upper
        | t -> fail (sprintf "Unexpected token in expression: %A" t)

    // --- Statements --------------------------------------------------------

    let private isKeyword (s: State) name =
        asKeyword (peek s) = Some name

    let private atStatementEnd (s: State) =
        match peek s with
        | NewLine | Colon | Eof -> true
        | _ -> false

    let private printTerminator name =
        match name with
        | "ELSE" | "THEN" | "TO" | "STEP" | "GOTO" | "GOSUB" | "LET" | "IF"
        | "FOR" | "NEXT" | "RETURN" | "STOP" | "END" | "CLS" | "INPUT" | "DIM"
        | "PAUSE" | "RUN" | "RANDOMIZE" -> true
        | _ -> false

    let private parseExprList (s: State) minCount =
        let items = ResizeArray<Expr>()
        items.Add(parseExpr s)

        while peek s = Comma do
            advance s |> ignore
            items.Add(parseExpr s)

        if items.Count < minCount then
            fail "Not enough arguments"

        List.ofSeq items

    let rec private parseStmtList (s: State) (stopOnElse: bool) : Stmt list =
        let stmts = ResizeArray<Stmt>()

        let mutable go = true

        while go do
            if atStatementEnd s then
                go <- false
            elif stopOnElse && isKeyword s "ELSE" then
                go <- false
            else
                stmts.Add(parseStatement s)

                if peek s = Colon then
                    advance s |> ignore
                elif atStatementEnd s then
                    go <- false
                elif stopOnElse && isKeyword s "ELSE" then
                    go <- false
                else
                    fail "Expected ':' or end of line"

        List.ofSeq stmts

    and private parseStatement (s: State) : Stmt =
        match peek s with
        | RemLit text ->
            advance s |> ignore
            Rem text
        | Ident name ->
            match name.ToUpperInvariant() with
            | "LET" ->
                advance s |> ignore
                parseAssignment s
            | "PRINT" ->
                advance s |> ignore
                parsePrint s
            | "LPRINT" ->
                advance s |> ignore
                parsePrint s
            | "INPUT" ->
                advance s |> ignore
                parseInput s
            | "IF" ->
                advance s |> ignore
                parseIf s
            | "FOR" ->
                advance s |> ignore
                parseFor s
            | "NEXT" ->
                advance s |> ignore
                parseNext s
            | "GOTO" ->
                advance s |> ignore
                Goto(parseExpr s)
            | "GOSUB" ->
                advance s |> ignore
                Gosub(parseExpr s)
            | "RETURN" ->
                advance s |> ignore
                Return
            | "RUN" ->
                advance s |> ignore

                if atStatementEnd s then
                    Run None
                else
                    Run(Some(parseExpr s))
            | "RANDOMIZE" ->
                advance s |> ignore

                if atStatementEnd s then
                    Randomize None
                else
                    Randomize(Some(parseExpr s))
            | "DIM" ->
                advance s |> ignore
                parseDim s
            | "STOP" ->
                advance s |> ignore
                Stop
            | "END" ->
                advance s |> ignore
                End
            | "PAUSE" ->
                advance s |> ignore

                if atStatementEnd s then
                    Pause None
                else
                    Pause(Some(parseExpr s))
            | "CLS" ->
                advance s |> ignore
                Cls
            | "PLOT" ->
                advance s |> ignore

                match parseExprList s 2 with
                | [ x; y ] -> Plot(x, y)
                | _ -> fail "PLOT expects x,y"
            | "DRAW" ->
                advance s |> ignore

                match parseExprList s 2 with
                | [ x; y ] -> Draw(x, y)
                | _ -> fail "DRAW expects x,y"
            | "CIRCLE" ->
                advance s |> ignore

                match parseExprList s 3 with
                | [ x; y; r ] -> Circle(x, y, r)
                | _ -> fail "CIRCLE expects x,y,r"
            | "INK" ->
                advance s |> ignore
                Ink(parseExpr s)
            | "PAPER" ->
                advance s |> ignore
                Paper(parseExpr s)
            | "BORDER" ->
                advance s |> ignore
                Border(parseExpr s)
            | "OVER" ->
                advance s |> ignore
                Over(parseExpr s)
            | "INVERSE" ->
                advance s |> ignore
                Inverse(parseExpr s)
            | "AT" ->
                advance s |> ignore

                match parseExprList s 2 with
                | [ r; c ] -> At(r, c)
                | _ -> fail "AT expects row,column"
            | "BEEP" ->
                advance s |> ignore

                match parseExprList s 1 with
                | [ d ] -> Beep(d, Number 0.0)
                | [ d; p ] -> Beep(d, p)
                | _ -> fail "BEEP expects duration[,pitch]"
            | "SOUND" ->
                advance s |> ignore

                match parseExprList s 2 with
                | [ f; d ] -> Sound(f, d)
                | _ -> fail "SOUND expects frequency,duration"
            | _ -> parseAssignment s
        | _ -> fail (sprintf "Unexpected token at statement start: %A" (peek s))

    and private parseAssignment (s: State) : Stmt =
        match peek s with
        | Ident name ->
            advance s |> ignore

            if peek s <> Eq then
                fail (sprintf "Expected '=' after '%s'" name)

            advance s |> ignore
            let value = parseExpr s
            Let(name.ToUpperInvariant(), value)
        | t -> fail (sprintf "Expected a variable name, found %A" t)

    and private parsePrint (s: State) : Stmt =
        let items = ResizeArray<PrintElement>()

        let mutable go = true

        while go do
            match peek s with
            | Semi ->
                advance s |> ignore
                items.Add PSemi
            | Comma ->
                advance s |> ignore
                items.Add PComma
            | NewLine | Colon | Eof -> go <- false
            | Ident name ->
                let upper = name.ToUpperInvariant()

                match upper with
                | "AT" ->
                    advance s |> ignore

                    match parseExprList s 2 with
                    | [ r; c ] -> items.Add(PAt(r, c))
                    | _ -> fail "PRINT AT expects row,column"
                | "TAB" ->
                    advance s |> ignore
                    items.Add(PTab(parseExpr s))
                | "INK" ->
                    advance s |> ignore
                    items.Add(PInk(parseExpr s))
                | "PAPER" ->
                    advance s |> ignore
                    items.Add(PPaper(parseExpr s))
                | "OVER" ->
                    advance s |> ignore
                    items.Add(POver(parseExpr s))
                | "INVERSE" ->
                    advance s |> ignore
                    items.Add(PInverse(parseExpr s))
                | "BORDER" ->
                    advance s |> ignore
                    items.Add(PBorder(parseExpr s))
                | _ when printTerminator upper -> go <- false
                | _ -> items.Add(PExpr(parseExpr s))
            | _ -> items.Add(PExpr(parseExpr s))

        Print(List.ofSeq items)

    and private parseInput (s: State) : Stmt =
        let vars = ResizeArray<string>()

        let mutable go = true

        while go do
            match peek s with
            | Ident name ->
                advance s |> ignore
                vars.Add(name.ToUpperInvariant())

                if peek s = Comma then
                    advance s |> ignore
            | _ -> go <- false

        if vars.Count = 0 then
            fail "INPUT requires at least one variable"

        Input(List.ofSeq vars)

    and private parseIf (s: State) : Stmt =
        let cond = parseExpr s

        if not (isKeyword s "THEN") then
            fail "IF requires THEN"

        advance s |> ignore

        // Spectrum jump form:  IF <cond> THEN <line number>
        let thenBranch =
            match peek s with
            | NumLit n ->
                advance s |> ignore
                [ Goto(Number n) ]
            | _ -> parseStmtList s true

        let elseBranch =
            if isKeyword s "ELSE" then
                advance s |> ignore
                Some(parseStmtList s false)
            else
                None

        If(cond, thenBranch, elseBranch)

    and private parseFor (s: State) : Stmt =
        match peek s with
        | Ident name ->
            advance s |> ignore
            expectToken s Eq "Expected '=' in FOR"
            let fromE = parseExpr s

            if not (isKeyword s "TO") then
                fail "FOR requires TO"

            advance s |> ignore
            let toE = parseExpr s

            let stepE =
                if isKeyword s "STEP" then
                    advance s |> ignore
                    Some(parseExpr s)
                else
                    None

            For(name.ToUpperInvariant(), fromE, toE, stepE)
        | _ -> fail "FOR requires a variable"

    and private parseNext (s: State) : Stmt =
        match peek s with
        | Ident name ->
            advance s |> ignore
            Next(Some(name.ToUpperInvariant()))
        | _ -> Next None

    and private parseDim (s: State) : Stmt =
        let decls = ResizeArray<string * Expr list>()

        let mutable go = true

        while go do
            match peek s with
            | Ident name ->
                advance s |> ignore

                if peek s <> LParen then
                    fail "DIM requires parentheses"

                advance s |> ignore
                let dims = ResizeArray<Expr>()

                if peek s <> RParen then
                    dims.Add(parseExpr s)

                    while peek s = Comma do
                        advance s |> ignore
                        dims.Add(parseExpr s)

                expectToken s RParen "Expected ')'"
                decls.Add(name.ToUpperInvariant(), List.ofSeq dims)

                if peek s = Comma then
                    advance s |> ignore
                else
                    go <- false
            | _ -> go <- false

        Dim(List.ofSeq decls)

    // --- Program -----------------------------------------------------------

    let parse (source: string) : Program =
        let tokens = tokenize source |> List.toArray
        let s = { Tokens = tokens; Pos = 0 }
        let lines = ResizeArray<Line>()

        while peek s <> Eof do
            match peek s with
            | NewLine -> advance s |> ignore
            | NumLit n ->
                advance s |> ignore
                let number = int n
                let stmts = parseStmtList s false

                if peek s = NewLine then
                    advance s |> ignore
                elif peek s <> Eof then
                    fail "Expected end of line"

                lines.Add { Number = number; Statements = stmts }
            | t -> fail (sprintf "Expected a line number, found %A" t)

        { Lines = List.ofSeq lines }

    let parseLine (source: string) : Stmt list =
        let tokens = tokenize source |> List.toArray
        let s = { Tokens = tokens; Pos = 0 }
        let stmts = parseStmtList s false
        stmts
