namespace Basic.Core

open System
open System.IO
open System.Threading
open Basic.Core.Ast

module Interpreter =

    type Value =
        | VNum of float
        | VStr of string

    exception BasicRuntimeError of string

    type private ForFrame =
        { Var: string
          Limit: float
          Step: float
          ForPc: int }

    type private State =
        { Steps: (int * Stmt)[]
          LineStart: Collections.Generic.Dictionary<int, int>
          Out: TextWriter
          ReadLine: unit -> string
          SleepMs: int -> unit
          Screen: Screen.Screen
          Audio: Audio.Audio
          mutable Vars: Map<string, Value>
          mutable Arrays: Map<string, float[]>
          mutable ForStack: ForFrame list
          mutable GosubStack: int list
          mutable Rng: Random
          mutable Col: int }

    type private Signal =
        | Continue
        | JumpLine of int
        | SetPc of int
        | Restart of int option
        | Paused
        | RunStop

    // --- Value helpers -----------------------------------------------------

    let private formatNumber (n: float) =
        if Double.IsInfinity n || Double.IsNaN n then
            "?"
        elif n = Math.Floor n && Math.Abs n < 1e15 then
            sprintf "%d" (int64 n)
        else
            sprintf "%.9g" n

    let private asNum (v: Value) =
        match v with
        | VNum n -> n
        | VStr s -> raise (BasicRuntimeError(sprintf "Expected a number, found string \"%s\"" s))

    let private asStr (v: Value) =
        match v with
        | VStr s -> s
        | VNum n -> raise (BasicRuntimeError(sprintf "Expected a string, found number %s" (formatNumber n)))

    let private formatValue (v: Value) =
        match v with
        | VStr s -> s
        | VNum n -> formatNumber n

    // --- Program preparation ----------------------------------------------

    let private prepare (program: Program) =
        let sorted = program.Lines |> List.sortBy (fun l -> l.Number)
        let steps = ResizeArray<int * Stmt>()
        let lineStart = Collections.Generic.Dictionary<int, int>()

        for line in sorted do
            if not (lineStart.ContainsKey line.Number) then
                lineStart.[line.Number] <- steps.Count

            for stmt in line.Statements do
                steps.Add((line.Number, stmt))

        steps.ToArray(), lineStart

    // --- Expression evaluation --------------------------------------------

    let private getVar (st: State) name =
        match Map.tryFind name st.Vars with
        | Some v -> v
        | None -> VNum 0.0

    let rec private evalExpr (st: State) (expr: Expr) : Value =
        match expr with
        | Number n -> VNum n
        | Str s -> VStr s
        | Var name ->
            match name with
            | "PI" -> VNum Math.PI
            | "RND" -> VNum(st.Rng.NextDouble())
            | _ -> getVar st name
        | Unary(Neg, e) -> VNum(-(asNum (evalExpr st e)))
        | Unary(Not, e) -> VNum(if asNum (evalExpr st e) = 0.0 then 1.0 else 0.0)
        | Binary(op, l, r) -> evalBinary st op l r
        | Call(name, args) -> evalCall st name args

    and private evalBinary st op l r =
        match op with
        | Add ->
            let lv = evalExpr st l
            let rv = evalExpr st r

            match lv, rv with
            | VStr a, VStr b -> VStr(a + b)
            | _ -> VNum(asNum lv + asNum rv)
        | Sub -> VNum(asNum (evalExpr st l) - asNum (evalExpr st r))
        | Mul -> VNum(asNum (evalExpr st l) * asNum (evalExpr st r))
        | Div ->
            let d = asNum (evalExpr st r)

            if d = 0.0 then raise (BasicRuntimeError "Division by zero")

            VNum(asNum (evalExpr st l) / d)
        | Mod ->
            let b = asNum (evalExpr st r)

            if b = 0.0 then raise (BasicRuntimeError "Division by zero")

            let a = asNum (evalExpr st l)
            VNum(a - b * Math.Floor(a / b))
        | Eq
        | Ne
        | Lt
        | Le
        | Gt
        | Ge ->
            let lv = evalExpr st l
            let rv = evalExpr st r

            let cmp =
                match lv, rv with
                | VStr a, VStr b -> compare a b
                | _ -> compare (asNum lv) (asNum rv)

            let result =
                match op with
                | Eq -> cmp = 0
                | Ne -> cmp <> 0
                | Lt -> cmp < 0
                | Le -> cmp <= 0
                | Gt -> cmp > 0
                | Ge -> cmp >= 0
                | _ -> false

            VNum(if result then 1.0 else 0.0)
        | And ->
            VNum(if asNum (evalExpr st l) <> 0.0 && asNum (evalExpr st r) <> 0.0 then 1.0 else 0.0)
        | Or ->
            VNum(if asNum (evalExpr st l) <> 0.0 || asNum (evalExpr st r) <> 0.0 then 1.0 else 0.0)

    and private evalCall st name (args: Expr list) =
        let argNum i = asNum (evalExpr st args.[i])
        let argStr i = asStr (evalExpr st args.[i])
        let fn = name.TrimEnd('$')

        match fn with
        | "ABS" -> VNum(abs (argNum 0))
        | "INT" -> VNum(Math.Floor(argNum 0))
        | "SGN" -> VNum(float (sign (argNum 0)))
        | "SQR" -> VNum(sqrt (argNum 0))
        | "SIN" -> VNum(Math.Sin(argNum 0))
        | "COS" -> VNum(Math.Cos(argNum 0))
        | "TAN" -> VNum(Math.Tan(argNum 0))
        | "ASN" -> VNum(Math.Asin(argNum 0))
        | "ACS" -> VNum(Math.Acos(argNum 0))
        | "ATN" -> VNum(Math.Atan(argNum 0))
        | "LN" -> VNum(Math.Log(argNum 0))
        | "EXP" -> VNum(Math.Exp(argNum 0))
        | "LOG" -> VNum(Math.Log10(argNum 0))
        | "POW" -> VNum(argNum 0 ** argNum 1)
        | "PI" -> VNum(Math.PI)
        | "RND" -> VNum(st.Rng.NextDouble())
        | "LEN" -> VNum(float (argStr 0).Length)
        | "CHR" -> VStr(string (char (int (argNum 0))))
        | "CODE" ->
            let s = argStr 0
            VNum(if s.Length = 0 then 0.0 else float (int s.[0]))
        | "STR" -> VStr(formatNumber (argNum 0))
        | "VAL" ->
            match Double.TryParse(argStr 0, Globalization.CultureInfo.InvariantCulture) with
            | true, n -> VNum n
            | _ -> VNum 0.0
        | "LEFT" -> VStr((argStr 0).Substring(0, min (int (argNum 1)) (argStr 0).Length))
        | "RIGHT" ->
            let s = argStr 0
            VStr(s.Substring(max 0 (s.Length - int (argNum 1))))
        | "MID" ->
            let s = argStr 0
            let start = max 0 (int (argNum 1) - 1)

            if start >= s.Length then
                VStr ""
            else
                let avail = s.Length - start

                let take =
                    if args.Length >= 3 then
                        min (int (argNum 2)) avail
                    else
                        avail

                VStr(s.Substring(start, take))
        | _ -> raise (BasicRuntimeError(sprintf "Unknown function '%s'" name))

    // --- Output ------------------------------------------------------------

    let private emit (st: State) (text: string) =
        st.Out.Write text

        for ch in text do
            if ch = '\n' then st.Col <- 0 else st.Col <- st.Col + 1

    /// Writes to the console without moving the tracked print column (used by
    /// PRINT AT, which positions the cursor absolutely).
    let private emitRaw (st: State) (text: string) =
        st.Out.Write text

    let private emitPrint (st: State) (items: PrintElement list) =
        let mutable lastWasSep = false

        let sizeAttr value =
            let n = int (asNum (evalExpr st value)) % 16
            if n < 0 then n + 16 else n

        for item in items do
            match item with
            | PExpr e ->
                let text = formatValue (evalExpr st e)
                emit st text
                st.Screen.PutText text
                lastWasSep <- false
            | PSemi -> lastWasSep <- true
            | PComma ->
                let pad = 16 - (st.Col % 16)

                if pad > 0 then
                    let spaces = String(' ', pad)
                    emit st spaces
                    st.Screen.PutText spaces

                lastWasSep <- true
            | PAt(r, c) ->
                let rr = max 0 (min (Screen.rows - 1) (int (asNum (evalExpr st r))))
                let cc = max 0 (min (Screen.cols - 1) (int (asNum (evalExpr st c))))
                st.Screen.posRow <- rr
                st.Screen.posCol <- cc
                // Console approximation: AT moves to a column, overwriting
                // whatever is already on that line (like the Spectrum).
                emitRaw st "\r"
                emitRaw st (String(' ', cc))
                st.Col <- cc
                lastWasSep <- false
            | PTab e ->
                let cc = max 0 (min (Screen.cols - 1) (int (asNum (evalExpr st e))))

                if cc < st.Col then
                    emit st "\n"
                    st.Screen.PutText "\n"

                if cc > st.Col then
                    let spaces = String(' ', cc - st.Col)
                    emit st spaces
                    st.Screen.PutText spaces

                lastWasSep <- false
            | PInk e ->
                st.Screen.posInk <- sizeAttr e
                lastWasSep <- false
            | PPaper e ->
                st.Screen.posPaper <- sizeAttr e
                lastWasSep <- false
            | PBorder e ->
                st.Screen.posBorder <- sizeAttr e
                lastWasSep <- false
            | POver e ->
                st.Screen.posOver <- if asNum (evalExpr st e) = 0.0 then 0 else 1
                lastWasSep <- false
            | PInverse e ->
                st.Screen.posInverse <- if asNum (evalExpr st e) = 0.0 then 0 else 1
                lastWasSep <- false

        if not lastWasSep then
            emit st "\n"
            st.Screen.PutText "\n"

    // --- Statement execution ----------------------------------------------

    let private execAssignment (st: State) name value =
        st.Vars <- Map.add name value st.Vars

    let rec private execBlock (st: State) (pc: int) (stmts: Stmt list) : Signal =
        let mutable signal = Continue

        for s in stmts do
            if signal = Continue then
                signal <- execStmt st pc s

        signal

    and private execStmt (st: State) (pc: int) (stmt: Stmt) : Signal =
        match stmt with
        | Nop
        | Rem _ -> Continue
        | Let(name, e) ->
            execAssignment st name (evalExpr st e)
            Continue
        | Print items ->
            emitPrint st items
            Continue
        | Input vars ->
            let line = st.ReadLine ()

            let parts =
                line.Split([| ','; ' ' |], StringSplitOptions.RemoveEmptyEntries)

            let mutable idx = 0

            for v in vars do
                if v.EndsWith "$" then
                    execAssignment st v (VStr(if idx < parts.Length then parts.[idx] else ""))
                else
                    let value =
                        if idx < parts.Length then
                            match Double.TryParse(parts.[idx], Globalization.CultureInfo.InvariantCulture) with
                            | true, n -> n
                            | _ -> 0.0
                        else
                            0.0

                    execAssignment st v (VNum value)

                idx <- idx + 1

            Continue
        | Cls ->
            if Object.ReferenceEquals(st.Out, Console.Out) && not Console.IsOutputRedirected then
                try Console.Clear() with _ -> ()

            st.Col <- 0
            st.Screen.Cls ()
            Continue
        | Plot(x, y) ->
            let sx = int (asNum (evalExpr st x))
            let sy = int (asNum (evalExpr st y))
            st.Screen.Plot(sx, sy)
            st.Screen.LastX <- sx
            st.Screen.LastY <- sy
            Continue
        | Draw(x, y) ->
            let tx = int (asNum (evalExpr st x))
            let ty = int (asNum (evalExpr st y))
            st.Screen.LineTo(tx, ty)
            Continue
        | Circle(x, y, r) ->
            let cx = int (asNum (evalExpr st x))
            let cy = int (asNum (evalExpr st y))
            let cr = int (asNum (evalExpr st r))
            st.Screen.Circle(cx, cy, cr)
            Continue
        | Ink e ->
            let n = int (asNum (evalExpr st e)) % 16
            st.Screen.posInk <- if n < 0 then n + 16 else n
            Continue
        | Paper e ->
            let n = int (asNum (evalExpr st e)) % 16
            st.Screen.posPaper <- if n < 0 then n + 16 else n
            Continue
        | Border e ->
            let n = int (asNum (evalExpr st e)) % 16
            st.Screen.posBorder <- if n < 0 then n + 16 else n
            Continue
        | Over e ->
            let v = if asNum (evalExpr st e) = 0.0 then 0 else 1
            st.Screen.posOver <- v
            Continue
        | Inverse e ->
            let v = if asNum (evalExpr st e) = 0.0 then 0 else 1
            st.Screen.posInverse <- v
            Continue
        | At(r, c) ->
            st.Screen.posRow <- max 0 (min (Screen.rows - 1) (int (asNum (evalExpr st r))))
            st.Screen.posCol <- max 0 (min (Screen.cols - 1) (int (asNum (evalExpr st c))))
            Continue
        | Beep(d, p) ->
            let duration = asNum (evalExpr st d)
            let pitch = asNum (evalExpr st p)
            st.Audio.Beep(duration, pitch)
            Continue
        | Sound(f, d) ->
            let freq = asNum (evalExpr st f)
            let duration = asNum (evalExpr st d)
            st.Audio.Play(freq, duration)
            Continue
        | Stop -> Paused
        | End -> RunStop
        | Pause maybe ->
            let seconds = match maybe with Some e -> asNum (evalExpr st e) | None -> 1.0
            st.SleepMs (int (max 0.0 seconds * 1000.0))
            Continue
        | Goto e -> JumpLine(int (asNum (evalExpr st e)))
        | Gosub e ->
            st.GosubStack <- (pc + 1) :: st.GosubStack
            JumpLine(int (asNum (evalExpr st e)))
        | Return ->
            match st.GosubStack with
            | ret :: rest ->
                st.GosubStack <- rest
                SetPc ret
            | [] -> raise (BasicRuntimeError "RETURN without GOSUB")
        | Run maybe ->
            let target = maybe |> Option.map (fun e -> int (asNum (evalExpr st e)))
            Restart target
        | Randomize maybe ->
            st.Rng <-
                match maybe with
                | Some e ->
                    let seed = int (asNum (evalExpr st e))
                    if seed = 0 then Random(Environment.TickCount) else Random(seed)
                | None -> Random()

            Continue
        | If(cond, thenBranch, elseBranch) ->
            if asNum (evalExpr st cond) <> 0.0 then
                execBlock st pc thenBranch
            else
                match elseBranch with
                | Some b -> execBlock st pc b
                | None -> Continue
        | For(name, fromE, toE, stepE) ->
            let startV = asNum (evalExpr st fromE)
            let limit = asNum (evalExpr st toE)
            let step = match stepE with Some e -> asNum (evalExpr st e) | None -> 1.0
            execAssignment st name (VNum startV)
            st.ForStack <- st.ForStack @ [ { Var = name; Limit = limit; Step = step; ForPc = pc } ]
            Continue
        | Next _ ->
            match List.tryLast st.ForStack with
            | None -> raise (BasicRuntimeError "NEXT without FOR")
            | Some frame ->
                let next = asNum (getVar st frame.Var) + frame.Step
                execAssignment st frame.Var (VNum next)

                let finished =
                    if frame.Step >= 0.0 then next > frame.Limit else next < frame.Limit

                if finished then
                    st.ForStack <- st.ForStack |> List.take (st.ForStack.Length - 1)
                    Continue
                else
                    SetPc(frame.ForPc + 1)
        | Dim decls ->
            for name, dims in decls do
                let sizes = dims |> List.map (fun d -> int (asNum (evalExpr st d)))
                let total = sizes |> List.fold (*) 1 |> max 1
                st.Arrays <- Map.add name (Array.zeroCreate total) st.Arrays

            Continue

    // --- Steppable machine (for interactive front-ends) --------------------

    /// Executes a program a bounded number of statements at a time so a
    /// host (e.g. a game loop) can render between steps.
    type Machine
        (
            out: TextWriter,
            readLine: unit -> string,
            screen: Screen.Screen,
            audio: Audio.Audio,
            program: Program,
            ?sleep: int -> unit
        ) =
        let sleepFn = defaultArg sleep (fun ms -> Thread.Sleep ms)

        let initialSteps, initialLineStart = prepare program

        let mutable st =
            { Steps = initialSteps
              LineStart = initialLineStart
              Out = out
              ReadLine = readLine
              SleepMs = sleepFn
              Rng = Random()
              Screen = screen
              Audio = audio
              Vars = Map.empty
              Arrays = Map.empty
              ForStack = []
              GosubStack = []
              Col = 0 }

        let mutable pc = 0

        let mutable paused = false
        let mutable contPc: int option = None

        let prepareFor (program: Program) =
            let s2, l2 = prepare program
            st <- { st with Steps = s2; LineStart = l2 }
            pc <- 0
            paused <- false
            contPc <- None

        let resetProgramState () =
            st.Vars <- Map.empty
            st.Arrays <- Map.empty
            st.ForStack <- []
            st.GosubStack <- []
            st.Col <- 0
            paused <- false
            contPc <- None

        let resolvePc line =
            match st.LineStart.TryGetValue line with
            | true, i -> i
            | _ -> raise (BasicRuntimeError(sprintf "Undefined line %d" line))

        member _.Screen = screen
        member _.Audio = audio
        member _.IsRunning = pc < st.Steps.Length && not paused
        member _.IsPaused = paused

        /// Load and run a different program on this machine, keeping variables,
        /// screen and audio from previous runs (like a real BASIC session).
        member this.Run(program: Program) : unit =
            do prepareFor program

            while pc < st.Steps.Length && not paused do
                let _, stmt = st.Steps.[pc]

                match execStmt st pc stmt with
                | Continue -> pc <- pc + 1
                | SetPc target -> pc <- target
                | JumpLine line -> pc <- resolvePc line
                | Restart target ->
                    resetProgramState ()
                    pc <- match target with Some line -> resolvePc line | None -> 0
                | RunStop ->
                    contPc <- None
                    pc <- st.Steps.Length
                | Paused ->
                    contPc <- Some(pc + 1)
                    paused <- true

        /// Run up to `budget` statements. Returns true while still running.
        member _.Step(budget: int) : bool =
            let mutable count = 0

            while pc < st.Steps.Length && count < budget && not paused do
                let _, stmt = st.Steps.[pc]

                match execStmt st pc stmt with
                | Continue -> pc <- pc + 1
                | SetPc target -> pc <- target
                | JumpLine line -> pc <- resolvePc line
                | Restart target ->
                    resetProgramState ()
                    pc <- match target with Some line -> resolvePc line | None -> 0
                | RunStop ->
                    contPc <- None
                    pc <- st.Steps.Length
                | Paused ->
                    contPc <- Some(pc + 1)
                    paused <- true

                count <- count + 1

            pc < st.Steps.Length && not paused

        member this.RunToCompletion() =
            while this.IsRunning do
                this.Step(4096) |> ignore

        /// Resume from a STOP (Spectrum CONT).
        member this.Continue() =
            match contPc with
            | Some p when paused ->
                paused <- false
                contPc <- None
                pc <- p

                while pc < st.Steps.Length && not paused do
                    let _, stmt = st.Steps.[pc]

                    match execStmt st pc stmt with
                    | Continue -> pc <- pc + 1
                    | SetPc target -> pc <- target
                    | JumpLine line -> pc <- resolvePc line
                    | Restart target ->
                        resetProgramState ()
                        pc <- match target with Some line -> resolvePc line | None -> 0
                    | RunStop ->
                        contPc <- None
                        pc <- st.Steps.Length
                    | Paused ->
                        contPc <- Some(pc + 1)
                        paused <- true
            | _ -> ()

        /// Forget variables, arrays and any pending FOR/GOSUB (like NEW).
        member _.Reset() =
            st <-
                { st with
                    Steps = [||]
                    LineStart = Collections.Generic.Dictionary<int, int>()
                    Vars = Map.empty
                    Arrays = Map.empty
                    ForStack = []
                    GosubStack = []
                    Col = 0 }

            pc <- 0
            paused <- false
            contPc <- None

    // --- Run ---------------------------------------------------------------

    let runOn (out: TextWriter) (readLine: unit -> string) (screen: Screen.Screen) (audio: Audio.Audio) (program: Program) : unit =
        let machine = Machine(out, readLine, screen, audio, program)

        while machine.Step(System.Int32.MaxValue) do
            ()

        out.Flush ()

    let run (out: TextWriter) (readLine: unit -> string) (program: Program) : unit =
        runOn out readLine (Screen.Screen()) (Audio.Audio()) program

    let runSession (out: TextWriter) (readLine: unit -> string) (program: Program) : Screen.Screen * Audio.Audio =
        let screen = Screen.Screen()
        let audio = Audio.Audio()
        runOn out readLine screen audio program
        screen, audio

    let runWithScreen (out: TextWriter) (readLine: unit -> string) (program: Program) : Screen.Screen =
        fst (runSession out readLine program)

    let private runCapture source =
        let sb = Text.StringBuilder()
        use writer = new StringWriter(sb)
        let screen, audio = runSession writer (fun () -> "") (Parser.parse source)
        sb.ToString(), screen, audio

    let runStringWithScreen (source: string) : string * Screen.Screen =
        let output, screen, _ = runCapture source
        output, screen

    let runStringWithAudio (source: string) : string * Audio.Audio =
        let output, _, audio = runCapture source
        output, audio

    let runStringSession (source: string) : string * Screen.Screen * Audio.Audio =
        runCapture source

    let runString (source: string) =
        let sb = Text.StringBuilder()
        use writer = new StringWriter(sb)
        run writer (fun () -> "") (Parser.parse source)
        sb.ToString()
