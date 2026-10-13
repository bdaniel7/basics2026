# BASIC Spectrum

A child-friendly Sinclair (ZX Spectrum) style BASIC for drawing, sound and
numbers. Written in F# (.NET 10). Use it as an interactive terminal REPL, a
graphical window shell, or as a bootable Raspberry Pi console.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows, macOS or Linux (the GUI shell uses Raylib; the terminal only needs a
  console)

## Run the terminal REPL

The simplest way in:

```
dotnet run --project src/Basic.Cli -c Release
```

You get a `basic>` prompt. Type numbered lines to build a program, then `run`.
Type `exit` to leave.

```
BASIC environment - type numbered lines, then 'run'. Type 'exit' to leave.

basic> 10 print "HELLO!"
basic> 20 for i=1 to 3
basic> 30 print i*i
basic> 40 next i
basic> run
HELLO!
1
4
9
basic> print 2+2
4
basic> list
10 print "HELLO!"
20 for i=1 to 3
30 print i*i
40 next i
basic> exit
```

Keywords and variables work in lowercase or UPPERCASE — this is a friendlier
take on the real Spectrum, where every keyword was typed as one capital letter
(`P` for `PRINT`, `R` for `RUN`, ...). `print`, `PrInT` and `PRINT` are all
the same; only string text keeps its case (`"Hello"` stays `Hello`).

Variables, graphics and sound carry over between direct commands and `run`,
just like the real machine (`new` clears them).

### Environment commands

| Typed input                | What it does                                  |
|----------------------------|-----------------------------------------------|
| `10 PRINT "HI"`            | stores a program line (replaces 10)           |
| `run`                      | runs the stored program                       |
| `list`                     | shows the stored program                      |
| `cont`                     | continues after `STOP` (REPL-only)            |
| `new`                      | deletes the program and resets variables      |
| `help`                     | lists the instructions you can type           |
| `PRINT 2+2`, `LET A=5`, ...| runs as a direct command immediately         |
| `exit` / `quit`            | leaves the REPL (Ctrl+Z then Enter also works)|

> `INPUT` statements prompt with `? ` inside the REPL and wait for your answer.

> Control flow: `IF <cond> THEN <line>` jumps to the line,
> `GOTO`/`GOSUB` accept any expression (e.g. `GOTO N * 10`), `RUN`/`RANDOMIZE`
> can take an optional value (`RUN 100`, `RANDOMIZE 42`), and `STOP` pauses the
> program until you type `cont`.

> Math functions: `SIN`, `COS`, `TAN`, `ASN`, `ACS`, `ATN` (radians),
> `LN`, `EXP`, `LOG` (base 10), `MOD` (remainder), plus the existing `ABS`,
> `INT`, `SGN`, `SQR`, `PI`, `RND` and the string functions.

> PRINT controls: `PRINT AT r,c; ...` moves the print position (overwriting),
> `PRINT TAB n; ...` skips to a column, and `INK`/`PAPER`/`OVER`/`INVERSE`/
> `BORDER` work inside PRINT to colour what is printed. `LPRINT` prints the
> same way (a friendly stand-in for a printer). `,` in PRINT advances to the
> next tab column (every 16 columns), `;` keeps printing on the same line.

> Programs that draw (`PLOT`, `DRAW`, `CIRCLE`, ...) print an ASCII preview of
> the 256x192 screen in the terminal after `run` (and after a drawing command
> typed directly).

## Run a saved program directly

Pass a `.bas` file as the only argument and it runs without entering the REPL:

```
basic programs/hello.bas      # from wherever you are
basic hello.bas               # a bare filename is found in programs/
```

The `programs/` examples are copied next to the executable when you build or
publish. A relative path is tried against your current directory first, then
against the folder the program lives in. A bad path prints `File not found`
and exits with code 1 (it no longer falls back to the REPL).

## Install a global `basic` command (Windows)

So you can type `basic` from any terminal:

```
pwsh -File packaging/windows/publish-windows.ps1
```

This publishes the REPL to `dist/basic` and drops a `basic.cmd` launcher next
to it. Add that folder to your `PATH` (e.g. `setx PATH "%PATH%;D:\projects\...\dist\basic"`,
then reopen terminals). After that:

```
basic
```

## Other ways to run it

```text
dotnet run --project src/Basic.Cli -- <file.bas>       # run a saved program, text output
dotnet run --project src/Basic.Cli -- run <file.bas>   # same as above
dotnet run --project src/Basic.Cli -- gfx <file.bas>   # ASCII-art preview + out.ppm
dotnet run --project src/Basic.Cli -- snd <file.bas>   # writes out.wav (audio preview)
dotnet run --project src/Basic.Cli -- -e "10 PRINT 2+2" # inline program
dotnet run --project src/Basic.Pi -c Release            # graphical window shell (Windows/macOS/Linux)
```

Example programs live in `programs/`: `hello.bas`, `rainbow.bas`, `shapes.bas`
(a drawing demo), `tune.bas`. Try drawing in the terminal:

```
basic gfx programs/shapes.bas    # ASCII preview + writes out.ppm
basic programs/shapes.bas        # just prints, no preview
```

### Raspberry Pi

`packaging/pi/README.md` explains how to build a self-contained arm64 bundle and
install it on 64-bit Raspberry Pi OS so the machine boots straight into the
BASIC shell. See `packaging/pi/build-on-windows.ps1` (build the bundle) and
`packaging/pi/install.sh` (install on the Pi).

## Development

```
dotnet build basic-spectrum.slnx -c Release   # build everything
dotnet test basic-spectrum.slnx -c Release    # run the test suite
```

Projects:

- `src/Basic.Core` – lexer, parser and interpreter (also does the 256x192
  graphics buffer and audio synthesis)
- `src/Basic.Cli` – the terminal REPL and file commands
- `src/Basic.Pi` – the graphical Raylib shell (also runs on Windows)
- `tests/Basic.Core.Tests` – unit tests