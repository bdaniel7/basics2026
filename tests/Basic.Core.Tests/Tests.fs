module Basic.Core.Tests.Tests

open System
open System.IO
open Xunit
open Basic.Core
open Basic.Core.Interpreter

let private runInput (input: string list) source =
    let sb = Text.StringBuilder()
    use writer = new StringWriter(sb)
    let lines = List.toArray input
    let mutable idx = 0
    let readLine () = if idx < lines.Length then lines.[idx] else ""
    run writer readLine (Parser.parse source)
    sb.ToString()

let private run' source = runInput [] source

[<Fact>]
let ``print literal`` () =
    Assert.Equal("HELLO\n", run' "10 PRINT \"HELLO\"")

[<Fact>]
let ``print separated by semicolon stays on line`` () =
    Assert.Equal("1", run' "10 PRINT 1;")

[<Fact>]
let ``print comma advances to tab column`` () =
    Assert.Equal("1" + String(' ', 15) + "2\n", run' "10 PRINT 1, 2")

[<Fact>]
let ``print tab positions at column`` () =
    Assert.Equal("     Z\n", run' "10 PRINT TAB(5); \"Z\"")

[<Fact>]
let ``print at overwrites and positions`` () =
    Assert.Equal("\r  X\n", run' "10 PRINT AT 3, 2; \"X\"")

[<Fact>]
let ``print at twice in one statement`` () =
    Assert.Equal("\rA\r    B\n", run' "10 PRINT AT 0,0; \"A\" AT 0,4; \"B\"")

[<Fact>]
let ``lprint prints like print`` () =
    Assert.Equal("HELLO\n", run' "10 LPRINT \"HELLO\"")

[<Fact>]
let ``print ink colours the screen buffer`` () =
    let output, screen = runStringWithScreen "10 PRINT INK 2; \"RED\""
    Assert.Equal("RED\n", output)
    Assert.Equal(2, screen.posInk)
    Assert.Equal(2, screen.GetPixel(0, 191))

[<Fact>]
let ``let and arithmetic`` () =
    Assert.Equal("7\n", run' "10 LET A = 3 + 4\n20 PRINT A")

[<Fact>]
let ``keywords are case-insensitive`` () =
    Assert.Equal("hi\n5\n", run' "10 print \"hi\"\n20 let A = 5\n30 print A")

[<Fact>]
let ``mixed case keywords work`` () =
    Assert.Equal("two\n", run' "10 If 1 = 1 THEN print \"two\"")

[<Fact>]
let ``variables are case-insensitive`` () =
    Assert.Equal("5\n", run' "10 let a = 5\n20 PRINT A")

[<Fact>]
let ``for next loop`` () =
    Assert.Equal("1\n2\n3\n", run' "10 FOR I = 1 TO 3\n20 PRINT I\n30 NEXT I")

[<Fact>]
let ``for step negative`` () =
    Assert.Equal("3\n2\n1\n", run' "10 FOR I = 3 TO 1 STEP -1\n20 PRINT I\n30 NEXT I")

[<Fact>]
let ``if then else`` () =
    Assert.Equal(
        "NO\nYES\nNO\n",
        run' "10 FOR I = 1 TO 3\n20 IF I / 2 = INT (I / 2) THEN PRINT \"YES\" ELSE PRINT \"NO\"\n30 NEXT I"
    )

[<Fact>]
let ``gosub return`` () =
    Assert.Equal("B\nA\n", run' "10 GOSUB 100\n20 PRINT \"A\"\n30 STOP\n100 PRINT \"B\"\n110 RETURN")

[<Fact>]
let ``goto`` () =
    Assert.Equal("only\n", run' "10 GOTO 30\n20 PRINT \"skip\"\n30 PRINT \"only\"")

[<Fact>]
let ``if then line number jumps`` () =
    Assert.Equal("TWO\n", run' "10 IF 1 <> 2 THEN 30\n20 PRINT \"ONE\"\n30 PRINT \"TWO\"")

[<Fact>]
let ``if then line number does not jump when false`` () =
    Assert.Equal("ONE\nTWO\n", run' "10 IF 1 = 2 THEN 30\n20 PRINT \"ONE\"\n30 PRINT \"TWO\"")

[<Fact>]
let ``computed goto`` () =
    Assert.Equal("got it\n", run' "10 LET N = 10\n20 GOTO N * 5\n30 PRINT \"no\"\n50 PRINT \"got it\"")

[<Fact>]
let ``computed gosub`` () =
    Assert.Equal("sub\nback\n", run' "10 LET N = 4\n20 GOSUB N * 25\n30 PRINT \"back\"\n40 STOP\n100 PRINT \"sub\"\n110 RETURN")

[<Fact>]
let ``run restarts from a line`` () =
    Assert.Equal("one\ntwo\n", run' "10 PRINT \"one\"\n20 RUN 50\n30 PRINT \"no\"\n50 PRINT \"two\"")

[<Fact>]
let ``randomize seeds rnd`` () =
    Assert.Equal("1\n", run' "10 RANDOMIZE 42\n20 LET A = RND\n30 RANDOMIZE 42\n40 PRINT RND = A")

[<Fact>]
let ``stop then continue resumes`` () =
    let sb = Text.StringBuilder()
    use writer = new StringWriter(sb)
    let machine =
        Machine(writer, (fun () -> ""), Screen.Screen(), Audio.Audio(),
                Parser.parse "10 PRINT \"one\"\n20 STOP\n30 PRINT \"two\"")

    machine.RunToCompletion()
    Assert.Equal("one\n", sb.ToString())
    machine.Continue()
    Assert.Equal("one\ntwo\n", sb.ToString())

[<Fact>]
let ``string concatenation`` () =
    Assert.Equal("KITTY CAT\n", run' "10 LET A$ = \"KITTY\"\n20 PRINT A$ + \" CAT\"")

[<Fact>]
let ``string functions`` () =
    Assert.Equal(
        "6\nA\n123\n5\nab\nde\nc\n67\n",
        run' "10 PRINT LEN(\"HEL LO\")\n20 PRINT CHR$(65)\n30 PRINT VAL(\"123\")\n40 PRINT STR$(5)\n50 PRINT LEFT$(\"abcde\", 2)\n60 PRINT RIGHT$(\"abcde\", 2)\n70 PRINT MID$(\"abcde\", 3, 1)\n80 PRINT CODE(\"C\")"
    )

[<Fact>]
let ``pi and powers`` () =
    Assert.Equal("8\n3\n", run' "10 PRINT 2 ^ 3\n20 PRINT INT (PI)")

[<Fact>]
let ``trig and inverse functions`` () =
    Assert.Equal(
        "0\n1\n0\n1\n1\n",
        run' "10 PRINT SIN(0)\n20 PRINT COS(0)\n30 PRINT TAN(0)\n40 PRINT ABS(SIN(ASN(0.5))-0.5)<0.000001\n50 PRINT ABS(COS(PI)+1)<0.000001"
    )

[<Fact>]
let ``logs and exponentials`` () =
    Assert.Equal("1\n0\n0\n3\n", run' "10 PRINT EXP(0)\n20 PRINT LN(1)\n30 PRINT LOG(1)\n40 PRINT INT(LN(EXP(3)) + 0.5)")
    Assert.Equal("2\n", run' "10 PRINT INT(LOG(100) + 0.5)")

[<Fact>]
let ``mod operator`` () =
    Assert.Equal("1\n2\n2\n", run' "10 PRINT 7 MOD 3\n20 PRINT 8 MOD 3\n30 PRINT (-7) MOD 3")

[<Fact>]
let ``comparison returns boolean`` () =
    Assert.Equal("1\n0\n", run' "10 PRINT 3 > 2\n20 PRINT 3 < 2")

[<Fact>]
let ``input assigns variables`` () =
    Assert.Equal("7\n", runInput [ "3 4" ] "10 INPUT A, B\n20 PRINT A + B")

[<Fact>]
let ``fizzbuzz contains bye`` () =
    let source = File.ReadAllText "samples/fizzbuzz.bas"
    let result = run' source
    Assert.Contains("FIZZ", result)
    Assert.Contains("BUZZ", result)
    Assert.Contains("BYE", result)

[<Fact>]
let ``division by zero errors`` () =
    Assert.Throws<BasicRuntimeError>(fun () -> run' "10 PRINT 1 / 0" |> ignore) |> ignore

[<Fact>]
let ``undefined goto errors`` () =
    Assert.Throws<BasicRuntimeError>(fun () -> run' "10 GOTO 999" |> ignore) |> ignore

[<Fact>]
let ``parse error is raised`` () =
    Assert.Throws<Parser.BasicSyntaxError>(fun () -> Parser.parse "10 PRINT (" |> ignore) |> ignore

// --- Graphics ----------------------------------------------------------------

[<Fact>]
let ``plot sets a pixel in the ink colour`` () =
    let _, screen = runStringWithScreen "10 INK 2\n20 PLOT 10, 10"
    Assert.Equal(2, screen.GetPixel(10, 10))

[<Fact>]
let ``plot uses y measured from the bottom`` () =
    let _, screen = runStringWithScreen "10 INK 2\n20 PLOT 10, 0"
    Assert.Equal(2, screen.GetPixel(10, 0))
    Assert.Equal(0, screen.GetPixel(10, 5))

[<Fact>]
let ``draw draws a line from the last point`` () =
    let _, screen = runStringWithScreen "10 INK 3\n20 PLOT 0, 0\n30 DRAW 100, 0"
    Assert.Equal(3, screen.GetPixel(50, 0))
    Assert.Equal(3, screen.GetPixel(100, 0))
    Assert.Equal(3, screen.GetPixel(0, 0))

[<Fact>]
let ``circle marks points at the radius`` () =
    let _, screen = runStringWithScreen "10 INK 5\n20 CIRCLE 128, 96, 60"
    Assert.Equal(5, screen.GetPixel(128 + 60, 96))
    Assert.Equal(5, screen.GetPixel(128, 96 + 60))

[<Fact>]
let ``cls fills the screen with paper`` () =
    let _, screen = runStringWithScreen "10 PAPER 1\n20 CLS\n30 INK 2\n40 PLOT 200, 100\n50 CLS"
    Assert.Equal(1, screen.GetPixel(200, 100))
    Assert.Equal(1, screen.GetPixel(0, 0))
    Assert.Equal(1, screen.GetPixel(255, 191))

[<Fact>]
let ``at positions text and print draws it`` () =
    let _, screen = runStringWithScreen "10 AT 5, 5\n20 PRINT \"A\""
    // Row 5 top-based => y 151 in bottom-based coords; 'A' starts at col 41.
    Assert.Equal(7, screen.GetPixel(41, 151))
    Assert.Equal(0, screen.GetPixel(40, 151))

[<Fact>]
let ``border stores the border colour`` () =
    let _, screen = runStringWithScreen "10 BORDER 6"
    Assert.Equal(6, screen.posBorder)

[<Fact>]
let ``over toggles pixels`` () =
    let _, screen = runStringWithScreen "10 INK 7\n20 PLOT 50, 50\n30 OVER 1\n40 PLOT 50, 50"
    Assert.Equal(0, screen.GetPixel(50, 50))

[<Fact>]
let ``shapes sample runs and draws`` () =
    let result, screen = runStringWithScreen (File.ReadAllText "samples/shapes.bas")
    Assert.Contains("HELLO SPECTRUM!", result)
    Assert.Equal(6, screen.GetPixel(40, 150))
    Assert.Equal(6, screen.GetPixel(128 + 55, 95))

// --- Audio -------------------------------------------------------------------

let private decodeShorts (wav: byte[]) =
    let dataStart = 44
    let count = (wav.Length - dataStart) / 2
    [| for i in 0 .. count - 1 -> BitConverter.ToInt16(wav, dataStart + i * 2) |]

let private zeroCrossings (samples: int16[]) =
    let mutable count = 0

    for i in 1 .. samples.Length - 1 do
        if (samples.[i - 1] < 0s) <> (samples.[i] < 0s) then
            count <- count + 1

    count

[<Fact>]
let ``beep renders the expected duration`` () =
    let _, audio = runStringWithAudio "10 BEEP 2, 0"
    Assert.Equal(2.0, audio.Duration, 3)
    Assert.Equal(1, audio.ToneCount)

[<Fact>]
let ``wav header is well formed`` () =
    let _, audio = runStringWithAudio "10 BEEP 1, 0"
    let wav = audio.ToWav()
    Assert.Equal("RIFF", Text.Encoding.ASCII.GetString(wav, 0, 4))
    Assert.Equal("WAVE", Text.Encoding.ASCII.GetString(wav, 8, 4))
    Assert.Equal("data", Text.Encoding.ASCII.GetString(wav, 36, 4))
    let totalSamples = 44100
    Assert.Equal(44 + totalSamples * 2, wav.Length)

[<Fact>]
let ``beep pitch an octave up doubles frequency`` () =
    let _, low = runStringWithAudio "10 BEEP 1, 0"
    let _, high = runStringWithAudio "10 BEEP 1, 12"
    let zlow = zeroCrossings (decodeShorts (low.ToWav()))
    let zhigh = zeroCrossings (decodeShorts (high.ToWav()))
    let ratio = float zhigh / float zlow
    Assert.InRange(ratio, 1.7, 2.3)

[<Fact>]
let ``sequential beeps add their durations`` () =
    let _, audio = runStringWithAudio "10 BEEP 1, 0\n20 BEEP 1, 0"
    Assert.Equal(2.0, audio.Duration, 3)
    Assert.Equal(2, audio.ToneCount)

[<Fact>]
let ``sound uses absolute frequency`` () =
    let _, audio = runStringWithAudio "10 SOUND 440, 0.5"
    Assert.Equal(0.5, audio.Duration, 3)
    Assert.Equal(1, audio.ToneCount)

[<Fact>]
let ``music sample renders tones`` () =
    let result, audio = runStringWithAudio (File.ReadAllText "samples/music.bas")
    Assert.Contains("THE END", result)
    Assert.True(audio.ToneCount >= 8)
    Assert.True(audio.Duration > 2.0)