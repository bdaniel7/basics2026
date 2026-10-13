#nowarn "3391"

namespace Basic.Pi

open System
open System.IO
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Basic.Core

type Mode =
    | Menu
    | Editing
    | Running
    | LoadMenu
    | Help

/// The child-friendly shell: a boot menu, a line-numbered editor and a live
/// run loop that blits the BASIC framebuffer to the window.
type App() =
    let mutable screenScale = 3
    let mutable screenH = Screen.height * screenScale
    let consoleH = 240
    let mutable windowW = Screen.width * screenScale
    let mutable windowH = screenH + consoleH
    let lineHeight = 22

    let editor = Editor()
    let synth = Synth(44100)
    let inputQueue = Queue<string>()

    let mutable mode = Menu
    let mutable quit = false
    let mutable inputBuf = ""
    let mutable transcript: string list = []
    let mutable screen = Screen.Screen()
    let mutable machine: Interpreter.Machine option = None
    let mutable files: string list = []
    let mutable texture = Unchecked.defaultof<Texture2D>
    let mutable audioStream = Unchecked.defaultof<AudioStream>

    let programsDir =
        let d = Path.Combine(AppContext.BaseDirectory, "programs")
        Directory.CreateDirectory d |> ignore
        d

    // --- raylib helpers (wrap CBool -> bool) ------------------------------

    let pressed (k: KeyboardKey) : bool = Raylib.IsKeyPressed k
    let charPressed () : int = Raylib.GetCharPressed()
    let windowShouldClose () : bool = Raylib.WindowShouldClose()

    // --- state helpers ----------------------------------------------------

    let log (msg: string) =
        transcript <- (transcript @ [ msg ]) |> List.rev |> List.truncate 6 |> List.rev

    let refreshFiles () =
        files <-
            Directory.GetFiles(programsDir, "*.bas")
            |> Array.map Path.GetFileNameWithoutExtension
            |> Array.sort
            |> Array.toList

    let reloadFiles () =
        refreshFiles ()
        mode <- LoadMenu

    let startRun () =
        if editor.Count = 0 then
            log "Nothing to run - try  10 PRINT \"HI\""
        else
            try
                let program = Parser.parse (editor.ProgramText)
                screen <- Screen.Screen()
                let audio = Audio.Audio()
                audio.Played.Add(fun (freq, dur) -> synth.Enqueue(freq, dur))
                synth.Clear ()
                inputQueue.Clear ()
                let readLine () = if inputQueue.Count > 0 then inputQueue.Dequeue() else ""
                machine <-
                    Some(
                        Interpreter.Machine(
                            TextWriter.Null,
                            readLine,
                            screen,
                            audio,
                            program,
                            sleep = (fun _ -> ())
                        )
                    )
                mode <- Running
            with
            | Parser.BasicSyntaxError msg -> log ("Syntax error: " + msg)
            | Lexer.BasicLexError msg -> log ("Lexical error: " + msg)
            | Interpreter.BasicRuntimeError msg -> log ("Runtime error: " + msg)

    // --- input ------------------------------------------------------------

    let keyDigit () =
        let mutable d = -1

        if pressed KeyboardKey.One then d <- 1
        elif pressed KeyboardKey.Two then d <- 2
        elif pressed KeyboardKey.Three then d <- 3
        elif pressed KeyboardKey.Four then d <- 4
        elif pressed KeyboardKey.Five then d <- 5
        elif pressed KeyboardKey.Six then d <- 6
        elif pressed KeyboardKey.Seven then d <- 7
        elif pressed KeyboardKey.Eight then d <- 8
        elif pressed KeyboardKey.Nine then d <- 9
        elif pressed KeyboardKey.Zero then d <- 0

        d

    let typeInto (buf: string) =
        let mutable b = buf
        let mutable c = charPressed ()

        while c > 0 do
            if c >= 32 && c < 127 then
                b <- b + string (char c)

            c <- charPressed ()

        if pressed KeyboardKey.Backspace && b.Length > 0 then
            b.Substring(0, b.Length - 1)
        else
            b

    let handleEditing () =
        if pressed KeyboardKey.Escape then
            mode <- Menu
        else
            inputBuf <- typeInto inputBuf

            if pressed KeyboardKey.Enter then
                match editor.Submit inputBuf with
                | SetLine _ -> inputBuf <- ""
                | Command("RUN", _) -> inputBuf <- ""; startRun ()
                | Command("LIST", _) ->
                    for n, t in editor.Lines do
                        log (sprintf "%d %s" n t)

                    inputBuf <- ""
                | Command("NEW", _) ->
                    editor.Clear ()
                    log "New program"
                    inputBuf <- ""
                | Command("SAVE", name :: _) ->
                    File.WriteAllText(Path.Combine(programsDir, name + ".bas"), editor.ProgramText)
                    log ("Saved " + name)
                    inputBuf <- ""
                | Command("SAVE", _) -> inputBuf <- ""
                | Command("LOAD", _) -> inputBuf <- ""; reloadFiles ()
                | Command("HELP", _) -> inputBuf <- ""; mode <- Help
                | Command(cmd, _) -> log ("Unknown command " + cmd); inputBuf <- ""
                | Nothing -> inputBuf <- ""

    let handleMenu () =
        match keyDigit () with
        | 1 -> mode <- Editing
        | 2 -> reloadFiles ()
        | 3 -> mode <- Help
        | 4 -> quit <- true
        | _ -> ()

    let handleLoadMenu () =
        if pressed KeyboardKey.Escape then
            mode <- Menu
        else
            match keyDigit () with
            | -1 -> ()
            | d ->
                let idx = (if d = 0 then 10 else d) - 1

                if idx < files.Length then
                    let path = Path.Combine(programsDir, files.[idx] + ".bas")
                    editor.LoadText(File.ReadAllText path)
                    log ("Loaded " + files.[idx])
                    mode <- Editing

    let handleRunning () =
        inputBuf <- typeInto inputBuf

        if pressed KeyboardKey.Enter then
            inputQueue.Enqueue inputBuf
            log ("? " + inputBuf)
            inputBuf <- ""

        let finished =
            match machine with
            | Some m when m.IsRunning -> false
            | _ -> true

        if finished && pressed KeyboardKey.Space then
            mode <- Editing

        if pressed KeyboardKey.Escape then
            machine <- None
            mode <- Editing

    // --- update -----------------------------------------------------------

    let pumpAudio () =
        let processed: bool = Raylib.IsAudioStreamProcessed audioStream

        if processed then
            let buf = Array.zeroCreate<float32> 1024
            synth.Fill buf
            Raylib.UpdateAudioStream(audioStream, ReadOnlySpan<float32> buf, buf.Length)

    let stepMachine () =
        match machine with
        | Some m when m.IsRunning ->
            try
                m.Step 2000 |> ignore
            with Interpreter.BasicRuntimeError msg -> log ("Runtime error: " + msg)
        | _ -> ()

    let update () =
        match mode with
        | Menu -> handleMenu ()
        | Editing -> handleEditing ()
        | Running ->
            stepMachine ()
            handleRunning ()
        | LoadMenu -> handleLoadMenu ()
        | Help ->
            if pressed KeyboardKey.Escape then
                mode <- Menu

        pumpAudio ()

    // --- draw -------------------------------------------------------------

    let shownLines () : string list =
        match mode with
        | Menu ->
            [ "  BASIC SPECTRUM"
              ""
              "  1  Type a program"
              "  2  Load a program"
              "  3  Help"
              "  4  Quit"
              ""
              "  (number keys to choose)" ]
        | Editing ->
            let programLines = editor.Lines |> List.map (fun (n, t) -> sprintf "%d %s" n t)
            [ "Ready - type a line, or RUN / LIST / NEW / SAVE name / LOAD  (ESC=menu)" ]
            @ transcript
            @ programLines
            @ [ "> " + inputBuf ]
        | Running ->
            let status =
                match machine with
                | Some m when m.IsRunning -> "Running...  (ESC=stop)"
                | _ -> "Finished - press SPACE"

            [ status; "Type and press ENTER to answer INPUT." ] @ transcript @ [ "> " + inputBuf ]
        | LoadMenu ->
            [ "Load which program?  (ESC=back)" ]
            @ (files |> List.mapi (fun i name -> sprintf "  %d  %s" (i + 1) name))
        | Help ->
            [ "BASIC SPECTRUM - Help  (ESC=back)"
              ""
              "  Type lines with numbers, e.g."
              "    10 PRINT \"HELLO\""
              "    20 FOR I=1 TO 10"
              "    30 PLOT I, I"
              "    40 NEXT I"
              "  Then type RUN to run it."
              ""
              "  Draw:  PLOT x,y  DRAW x,y  CIRCLE x,y,r"
              "  Sound: BEEP seconds, pitch"
              "  Colours: INK n  PAPER n  BORDER n" ]

    let draw () =
        Raylib.BeginDrawing()
        Raylib.ClearBackground(Color.Black)

        let colours = Renderer.toColours screen
        Raylib.UpdateTexture(texture, colours)
        Raylib.DrawTextureEx(texture, Vector2.Zero, 0.0f, float32 screenScale, Color.White)

        Raylib.DrawRectangle(0, screenH, windowW, consoleH, Color(12, 8, 32, 255))
        Raylib.DrawRectangleLines(0, screenH, windowW, consoleH, Color(90, 90, 140, 255))

        shownLines ()
        |> List.truncate ((consoleH - 12) / lineHeight)
        |> List.iteri (fun i l ->
            Raylib.DrawText(l, 8, screenH + 6 + i * lineHeight, 18, Color.RayWhite))

        Raylib.EndDrawing()

    // --- main loop --------------------------------------------------------

    member this.Run() =
        Raylib.InitWindow(windowW, windowH, "BASIC Spectrum")

        // Pick the largest integer scale that still leaves room for the console
        // on the actual monitor, so the app fits small (720p) and large screens.
        let monitorH = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor())

        if monitorH > 0 then
            let available = monitorH - consoleH - 40
            screenScale <- max 1 (min 5 (available / Screen.height))
            screenH <- Screen.height * screenScale
            windowW <- Screen.width * screenScale
            windowH <- screenH + consoleH
            Raylib.SetWindowSize(windowW, windowH)

        Raylib.SetTargetFPS 60

        let img = Raylib.GenImageColor(Screen.width, Screen.height, Color.Black)
        texture <- Raylib.LoadTextureFromImage img
        Raylib.UnloadImage img
        Raylib.SetTextureFilter(texture, TextureFilter.Point)

        Raylib.InitAudioDevice()
        audioStream <- Raylib.LoadAudioStream(44100u, 32u, 1u)
        Raylib.PlayAudioStream audioStream

        refreshFiles ()

        while not quit && not (windowShouldClose ()) do
            update ()
            draw ()

        Raylib.UnloadAudioStream audioStream
        Raylib.CloseAudioDevice()
        Raylib.UnloadTexture texture
        Raylib.CloseWindow()