module Basic.Cli.Program

open System
open System.IO
open System.Text
open Basic.Core

let private banner =
    "Sinclair-style BASIC for children\n" +
    "  basic <file.bas>      |   basic run <file.bas>   |   basic -e <source>\n" +
    "  basic gfx <file.bas>   |   basic snd <file.bas>\n" +
    "  basic                  - start the interactive environment, type 'exit' to leave\n" +
    "  Within the environment: 10 PRINT \"HI\"  ->  run  ->  list  ->  new  ->  exit\n"

let private printError (kind: string) (msg: string) =
    eprintfn "%s: %s" kind msg

let private resolveRunFile (path: string) =
    let leafOnly = not (path.Contains(Path.DirectorySeparatorChar) || path.Contains('/'))

    [ Some path
      Some(Path.Combine(AppContext.BaseDirectory, path))
      if leafOnly then Some(Path.Combine(AppContext.BaseDirectory, "programs", path)) ]
    |> List.choose id
    |> List.tryFind File.Exists

let private runFile (path: string) : int =
    match resolveRunFile path with
    | Some fp ->
        try
            Interpreter.runString (File.ReadAllText fp) |> Console.Out.Write
        with
        | Parser.BasicSyntaxError msg -> printError "Syntax error" msg
        | Lexer.BasicLexError msg -> printError "Lexical error" msg
        | Interpreter.BasicRuntimeError msg -> printError "Runtime error" msg

        Console.Out.Flush()
        0
    | None ->
        eprintfn "File not found: %s (%s)" path AppContext.BaseDirectory
        Console.Out.Flush()
        1

let private consoleReadLine () : string =
    printf "? "
    Console.Out.Flush()

    try
        match Console.ReadLine() with
        | null -> ""
        | s -> s
    with :? IOException ->
        ""

let private startEnvironment () =
    let program = ResizeArray<int * string>()

    let buildSource () =
        program
        |> Seq.sortBy fst
        |> Seq.map (fun (n, body) -> sprintf "%d %s" n body)
        |> String.concat "\n"

    // One interpreter shared by every command (and RUN), so variables,
    // graphics and audio behave like a real Sinclair BASIC session.
    let machine = Interpreter.Machine(Console.Out, consoleReadLine, Screen.Screen(), Audio.Audio(), { Lines = [] })

    let renderScreenIfDirty () =
        if machine.Screen.Dirty then
            let art = machine.Screen.ToAsciiArt 4

            if art |> List.exists (fun l -> l.Trim().Length > 0) then
                printfn ""
                for l in art do
                    printfn "%s" l

            machine.Screen.Dirty <- false

    let runProgram (lines: Ast.Line list) =
        try
            machine.Run { Lines = lines }
            renderScreenIfDirty ()
        with
        | Parser.BasicSyntaxError msg -> printError "Syntax error" msg
        | Lexer.BasicLexError msg -> printError "Lexical error" msg
        | Interpreter.BasicRuntimeError msg -> printError "Runtime error" msg

    let listProgram () =
        if program.Count = 0 then
            printfn "no program yet"
        else
            for n, body in program |> Seq.sortBy fst do
                printfn "%d %s" n body

    let runStored () =
        if program.Count = 0 then
            printfn "nothing to run - type a numbered line first, e.g.:  10 PRINT \"HI\""
        else
            match (try Choice1Of2(Parser.parse (buildSource ())) with e -> Choice2Of2 e) with
            | Choice1Of2 p -> runProgram p.Lines
            | Choice2Of2 (Parser.BasicSyntaxError msg) -> printError "Syntax error" msg
            | Choice2Of2 (Lexer.BasicLexError msg) -> printError "Lexical error" msg
            | Choice2Of2 _ -> ()

    let runDirect (line: string) =
        match (try Choice1Of2(Parser.parseLine line) with e -> Choice2Of2 e) with
        | Choice1Of2 stmts -> runProgram [ { Number = 1; Statements = stmts } ]
        | Choice2Of2 (Parser.BasicSyntaxError msg) -> printError "Syntax error" msg
        | Choice2Of2 (Lexer.BasicLexError msg) -> printError "Lexical error" msg
        | Choice2Of2 _ -> ()

    let showHelp () =
        printfn "HELLO! Here is what I can do:"
        printfn ""
        printfn "  Keywords work in lowercase or UPPERCASE:"
        printfn "    print 2+2     let x = 5     run"
        printfn ""
        printfn "  Draw and colour"
        printfn "    PLOT x,y          DRAW x,y           CIRCLE x,y,r"
        printfn "    CLS               INK n              PAPER n"
        printfn "    BORDER n          OVER 1             INVERSE 1"
        printfn "    AT row,col"
        printfn ""
        printfn "  Sound"
        printfn "    BEEP seconds,pitch"
        printfn ""
        printfn "  Numbers and text"
        printfn "    PRINT ...         LET value=...      INPUT ..."
        printfn "    FOR i=1 TO 10 [STEP n]  NEXT i"
        printfn "    IF ... THEN ... [ELSE ...]   (also: IF x THEN 100)"
        printfn "    GOTO n            GOSUB n            RETURN"
        printfn "    RUN [line]        RANDOMIZE [seed]   CONT"
        printfn "    REM ...           STOP               END"
        printfn "    LPRINT ...        (PRINT to the printer)"
        printfn ""
        printfn "  Print controls"
        printfn "    PRINT AT r,c; ...     move the print position"
        printfn "    PRINT TAB n; ...      skip to a column"
        printfn "    PRINT INK n; PAPER n; OVER 1; INVERSE 1; BORDER n"
        printfn ""
        printfn "  Functions"
        printfn "    ABS INT SGN SQR SIN COS TAN ASN ACS ATN"
        printfn "    LN EXP LOG   LEN CHR$ CODE STR$ VAL"
        printfn "    LEFT$ RIGHT$ MID$ PI RND   + - * / MOD ^ < > <= >= <>"
        printfn ""
        printfn "Type numbered lines (like  10 PRINT \"HI\"), then type:  run"

    printfn "BASIC environment - type numbered lines, then 'run'. Type 'exit' to leave."
    printfn ""

    let mutable exiting = false

    while not exiting do
        printf "basic> "
        Console.Out.Flush()

        match Console.ReadLine() with
        | null ->
            printfn ""
            exiting <- true
        | line ->
            let t = line.Trim()

            if t.Length > 0 then
                let idx = t.IndexOf ' '
                let first = if idx < 0 then t else t.Substring(0, idx)

                match Int32.TryParse first with
                | true, n ->
                    let body = (if idx < 0 then "" else t.Substring(idx + 1)).Trim()
                    program.RemoveAll(fun (num, _) -> num = n) |> ignore

                    if body.Length > 0 then
                        program.Add(n, body)
                | _ ->
                    match first.ToUpperInvariant() with
                    | "RUN" -> runStored ()
                    | "LIST" -> listProgram ()
                    | "NEW" ->
                        program.Clear()
                        machine.Reset()
                        printfn "new program"
                    | "CONT" ->
                        if machine.IsPaused then
                            machine.Continue()
                            renderScreenIfDirty ()
                        else
                            printfn "nothing to continue"
                    | "HELP" -> showHelp ()
                    | "EXIT" | "QUIT" -> exiting <- true
                    | _ -> runDirect t

[<EntryPoint>]
let main argv =
    match argv with
    | [| "run"; path |] -> runFile path
    | [| "gfx"; path |] ->
        if File.Exists path then
            try
                let screen =
                    Interpreter.runWithScreen Console.Out (fun () -> "") (Parser.parse (File.ReadAllText path))

                printfn ""
                for line in screen.ToAsciiArt 4 do
                    printfn "%s" line

                File.WriteAllBytes("out.ppm", screen.ToPpm 2)
                printfn "wrote out.ppm"
            with
            | Parser.BasicSyntaxError msg -> printError "Syntax error" msg
            | Lexer.BasicLexError msg -> printError "Lexical error" msg
            | Interpreter.BasicRuntimeError msg -> printError "Runtime error" msg
        else
            eprintfn "File not found: %s" path
        Console.Out.Flush()
        0
    | [| "snd"; path |] ->
        if File.Exists path then
            try
                let output, audio = Interpreter.runStringWithAudio (File.ReadAllText path)
                Console.Out.Write output
                File.WriteAllBytes("out.wav", audio.ToWav())
                printfn "wrote out.wav (%.2fs, %d tones)" audio.Duration audio.ToneCount
            with
            | Parser.BasicSyntaxError msg -> printError "Syntax error" msg
            | Lexer.BasicLexError msg -> printError "Lexical error" msg
            | Interpreter.BasicRuntimeError msg -> printError "Runtime error" msg
        else
            eprintfn "File not found: %s" path
        Console.Out.Flush()
        0
    | [| "-e"; source |] ->
        try
            Interpreter.runString source |> Console.Out.Write
        with
        | Parser.BasicSyntaxError msg -> printError "Syntax error" msg
        | Lexer.BasicLexError msg -> printError "Lexical error" msg
        | Interpreter.BasicRuntimeError msg -> printError "Runtime error" msg
        Console.Out.Flush()
        0
    | [| path |] -> runFile path
    | _ ->
        Console.Out.Flush()

        if argv.Length > 0 then
            printfn "Unknown arguments: %s" (String.concat " " argv)
            printfn "%s" banner
            Console.Out.Flush()
            2
        else
            startEnvironment ()
            Console.Out.Flush()
            0