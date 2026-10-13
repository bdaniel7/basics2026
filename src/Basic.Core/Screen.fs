namespace Basic.Core

open System
open System.IO
open System.Text

module Screen =

    let width = 256
    let height = 192

    /// Spectrum display: 32 columns x 24 rows of 8x8 character cells.
    let cols = 32
    let rows = 24

    /// Standard Spectrum palette. Indices 0-7 are normal colours, 8-15 bright.
    let private palette: (byte * byte * byte)[] =
        [| (0uy, 0uy, 0uy)
           (0uy, 0uy, 215uy)
           (215uy, 0uy, 0uy)
           (215uy, 0uy, 215uy)
           (0uy, 215uy, 0uy)
           (0uy, 215uy, 215uy)
           (215uy, 215uy, 0uy)
           (215uy, 215uy, 215uy)
           (0uy, 0uy, 0uy)
           (0uy, 0uy, 255uy)
           (255uy, 0uy, 0uy)
           (255uy, 0uy, 255uy)
           (0uy, 255uy, 0uy)
           (0uy, 255uy, 255uy)
           (255uy, 255uy, 0uy)
           (255uy, 255uy, 255uy) |]

    let colourName (i: int) =
        match i % 8 with
        | 0 -> "black"
        | 1 -> "blue"
        | 2 -> "red"
        | 3 -> "magenta"
        | 4 -> "green"
        | 5 -> "cyan"
        | 6 -> "yellow"
        | 7 -> "white"
        | _ -> "?"

    let rgb (i: int) = palette.[i % 16]

    /// 8x8 character font (upper-case, plus digits and punctuation).
    let private font: Map<char, string[]> =
        let g rows = rows
        let d = "........"

        Map.ofList [
            ' ', [|
                d; d; d; d; d; d; d; d
            |]
            'A', [|
                ".####.."
                "#....#."
                "#....#."
                "######."
                "#....#."
                "#....#."
                "#....#."
                d
            |]
            'B', [|
                "#####.."
                "#....#."
                "#....#."
                "#####.."
                "#....#."
                "#....#."
                "#####.."
                d
            |]
            'C', [|
                ".####.."
                "#....#."
                "#......"
                "#......"
                "#......"
                "#....#."
                ".####.."
                d
            |]
            'D', [|
                "#####.."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                "#####.."
                d
            |]
            'E', [|
                "######."
                "#......"
                "#......"
                "#####.."
                "#......"
                "#......"
                "######."
                d
            |]
            'F', [|
                "######."
                "#......"
                "#......"
                "#####.."
                "#......"
                "#......"
                "#......"
                d
            |]
            'G', [|
                ".####.."
                "#....#."
                "#......"
                "#..###."
                "#....#."
                "#....#."
                ".####.."
                d
            |]
            'H', [|
                "#....#."
                "#....#."
                "#....#."
                "######."
                "#....#."
                "#....#."
                "#....#."
                d
            |]
            'I', [|
                "###...."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                "###...."
                d
            |]
            'J', [|
                "..###.."
                "...#..."
                "...#..."
                "...#..."
                "...#..."
                "#..#..."
                ".##...."
                d
            |]
            'K', [|
                "#....#."
                "#...#.."
                "#..#..."
                "###...."
                "#..#..."
                "#...#.."
                "#....#."
                d
            |]
            'L', [|
                "#......"
                "#......"
                "#......"
                "#......"
                "#......"
                "#......"
                "######."
                d
            |]
            'M', [|
                "#....#."
                "##..##."
                "#.##.#."
                "#.##.#."
                "#....#."
                "#....#."
                "#....#."
                d
            |]
            'N', [|
                "#....#."
                "##...#."
                "#.#..#."
                "#..#.#."
                "#...#.#"
                "#....##"
                "#....#."
                d
            |]
            'O', [|
                ".####.."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                ".####.."
                d
            |]
            'P', [|
                "#####.."
                "#....#."
                "#....#."
                "#####.."
                "#......"
                "#......"
                "#......"
                d
            |]
            'Q', [|
                ".####.."
                "#....#."
                "#....#."
                "#....#."
                "#..#.#."
                "#...#.."
                ".####.."
                ".....#."
            |]
            'R', [|
                "#####.."
                "#....#."
                "#....#."
                "#####.."
                "#..#..."
                "#...#.."
                "#....#."
                d
            |]
            'S', [|
                ".####.."
                "#....#."
                "#......"
                ".####.."
                "......#"
                "#....#."
                ".####.."
                d
            |]
            'T', [|
                "#####.."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                d
            |]
            'U', [|
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                ".####.."
                d
            |]
            'V', [|
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                "#....#."
                ".#..#.."
                "..##..."
                d
            |]
            'W', [|
                "#....#."
                "#....#."
                "#....#."
                "#.##.#."
                "#.##.#."
                "##..##."
                "#....#."
                d
            |]
            'X', [|
                "#....#."
                "#....#."
                ".#..#.."
                "..#...."
                ".#..#.."
                "#....#."
                "#....#."
                d
            |]
            'Y', [|
                "#....#."
                "#....#."
                ".#..#.."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                d
            |]
            'Z', [|
                "######."
                ".....#."
                "....#.."
                "...#..."
                "..#...."
                ".#....."
                "######."
                d
            |]
            '0', [|
                ".####.."
                "#....#."
                "#...##."
                "#..#.#."
                "#.##.#."
                "##...#."
                ".####.."
                d
            |]
            '1', [|
                "..#...."
                ".##...."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                ".###..."
                d
            |]
            '2', [|
                ".####.."
                "#....#."
                ".....#."
                "...#..."
                "..#...."
                ".#....."
                "######."
                d
            |]
            '3', [|
                ".####.."
                "#....#."
                ".....#."
                "..###.."
                ".....#."
                "#....#."
                ".####.."
                d
            |]
            '4', [|
                "...#..."
                "..##..."
                ".#.#..."
                "#..#..."
                "######."
                "...#..."
                "...#..."
                d
            |]
            '5', [|
                "######."
                "#......"
                "#####.."
                ".....#."
                ".....#."
                "#....#."
                ".####.."
                d
            |]
            '6', [|
                "..###.."
                ".#....."
                "#......"
                "#####.."
                "#....#."
                "#....#."
                ".####.."
                d
            |]
            '7', [|
                "######."
                ".....#."
                "....#.."
                "...#..."
                "..#...."
                "..#...."
                "..#...."
                d
            |]
            '8', [|
                ".####.."
                "#....#."
                "#....#."
                ".####.."
                "#....#."
                "#....#."
                ".####.."
                d
            |]
            '9', [|
                ".####.."
                "#....#."
                "#....#."
                ".####.."
                ".....#."
                ".....#."
                "###...."
                d
            |]
            '.', [|
                d; d; d; d; d; "..##..."; "..##..."; d
            |]
            ',', [|
                d; d; d; d; d; "..##..."; "..##..."; "..#...."
            |]
            '!', [|
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                "..#...."
                d
                "..#...."
                d
            |]
            '?', [|
                ".###..."
                "#....#."
                ".....#."
                "...#..."
                "..#...."
                d
                "..#...."
                d
            |]
            ':', [|
                d; "..##..."; "..##..."; d; "..##..."; "..##..."; d; d
            |]
            ';', [|
                d; d; "...##.."; "...##.."; d; "...##.."; "...##.."; "..#...."
            |]
            '(', [|
                "...##.."
                "..#...."
                ".#....."
                ".#....."
                ".#....."
                ".#....."
                "..#...."
                "...##.."
            |]
            ')', [|
                ".##...."
                "...#..."
                "....#.."
                "....#.."
                "....#.."
                "....#.."
                "...#..."
                ".##...."
            |]
            '+', [|
                d; d; "...#..."; "######."; "...#..."; d; d; d
            |]
            '-', [|
                d; d; d; "######."; d; d; d; d
            |]
            '=', [|
                d; d; "######."; d; "######."; d; d; d
            |]
            '*', [|
                d
                "#....#."
                ".#..#.."
                "..##..."
                ".#..#.."
                "#....#."
                d
                d
            |]
            '/', [|
                ".......#"
                "......#."
                ".....#.."
                "....#..."
                "...#...."
                "..#....."
                ".#......"
                "#......."
            |]
            '\'', [|
                "..#...."; "..#...."; d; d; d; d; d; d
            |]
            '"', [|
                "..#..#.."; "..#..#.."; d; d; d; d; d; d
            |]
            '#', [|
                ".#..#.."
                ".#..#.."
                "######."
                ".#..#.."
                "######."
                ".#..#.."
                ".#..#.."
                d
            |]
            '<', [|
                "....##."
                "...#..."
                "..#...."
                ".#....."
                "..#...."
                "...#..."
                "....##."
                d
            |]
            '>', [|
                ".##...."
                "...#..."
                "....#.."
                ".....#."
                "....#.."
                "...#..."
                ".##...."
                d
            |]
            '@', [|
                ".####.."
                "#....#."
                ".....#."
                "..##.#."
                ".#.#.#."
                "#..#.#."
                ".####.."
                d
            |]
            '^', [|
                "..#...."; ".#.#..."; "#...#.."; d; d; d; d; d
            |]
            '_', [|
                d; d; d; d; d; d; d; "######."
            |]
            ' ', [|
                d; d; d; d; d; d; d; d
            |]
        ]

    let private glyph (c: char) =
        let upper = Char.ToUpperInvariant c

        match Map.tryFind upper font with
        | Some g -> g
        | None -> font.['?']

    /// A 256x192 indexed framebuffer with Spectrum-like text and graphics.
    type Screen() =
        let pixels = Array2D.zeroCreate<byte> height width

        member val posInk = 7 with get, set
        member val posPaper = 0 with get, set
        member val posBorder = 0 with get, set
        member val posOver = 0 with get, set
        member val posInverse = 0 with get, set
        member val posRow = 0 with get, set
        member val posCol = 0 with get, set
        member val LastX = 0 with get, set
        member val LastY = 0 with get, set
        member val Dirty = false with get, set

        member _.Width = width
        member _.Height = height

        member this.GetPixel(x: int, y: int) : int =
            // Graphics coordinates: y is measured from the bottom, like the Spectrum.
            let ry = height - 1 - y
            if x >= 0 && x < width && ry >= 0 && ry < height then
                int pixels.[ry, x]
            else
                0

        member this.Pixels = pixels

        member this.Plot(x: int, y: int) =
            let ry = height - 1 - y

            if x >= 0 && x < width && ry >= 0 && ry < height then
                let current = int pixels.[ry, x]

                let value =
                    if this.posOver = 1 then
                        if current = this.posInk then this.posPaper else this.posInk
                    else
                        this.posInk

                pixels.[ry, x] <- byte value
                this.Dirty <- true
            else
                ()

        member this.LineTo(x: int, y: int) =
            let x0 = this.LastX
            let y0 = this.LastY
            let dx = abs (x - x0)
            let sx = if x0 < x then 1 else -1
            let dy = -abs (y - y0)
            let sy = if y0 < y then 1 else -1
            let mutable err = dx + dy
            let mutable cx = x0
            let mutable cy = y0

            let mutable finished = false

            while not finished do
                this.Plot(cx, cy)

                if cx = x && cy = y then
                    finished <- true
                else
                    let e2 = 2 * err

                    if e2 >= dy then
                        err <- err + dy
                        cx <- cx + sx

                    if e2 <= dx then
                        err <- err + dx
                        cy <- cy + sy

            this.LastX <- x
            this.LastY <- y

        member this.Circle(cx: int, cy: int, r: int) =
            let plotSym x y =
                this.Plot(cx + x, cy + y)
                this.Plot(cx - x, cy + y)
                this.Plot(cx + x, cy - y)
                this.Plot(cx - x, cy - y)
                this.Plot(cx + y, cy + x)
                this.Plot(cx - y, cy + x)
                this.Plot(cx + y, cy - x)
                this.Plot(cx - y, cy - x)

            if r = 0 then
                this.Plot(cx, cy)
            else
                let mutable x = 0
                let mutable y = r
                let mutable d = 3 - 2 * r

                while y >= x do
                    plotSym x y
                    x <- x + 1

                    if d > 0 then
                        y <- y - 1
                        d <- d + 4 * (x - y) + 10
                    else
                        d <- d + 4 * x + 6

            this.LastX <- cx + r
            this.LastY <- cy

        member this.Cls() =
            for y in 0 .. height - 1 do
                for x in 0 .. width - 1 do
                    pixels.[y, x] <- byte this.posPaper

            this.posRow <- 0
            this.posCol <- 0

        member this.NewLine() =
            this.posCol <- 0
            this.posRow <- this.posRow + 1

            if this.posRow >= rows then
                for y in 0 .. height - 1 - 8 do
                    for x in 0 .. width - 1 do
                        pixels.[y, x] <- pixels.[y + 8, x]

                for y in height - 8 .. height - 1 do
                    for x in 0 .. width - 1 do
                        pixels.[y, x] <- byte this.posPaper

                this.posRow <- rows - 1

        member this.PutChar(ch: char) =
            if ch = '\n' then
                this.NewLine ()
            else
                let g = glyph ch
                let rowBase = this.posRow * 8
                let colBase = this.posCol * 8

                for r in 0 .. 7 do
                    let row = g.[r]

                    for c in 0 .. 7 do
                        let lit = c < row.Length && row.[c] = '#'

                        let value =
                            if lit then
                                if this.posInverse = 1 then this.posPaper else this.posInk
                            else
                                if this.posInverse = 1 then this.posInk else this.posPaper

                        pixels.[rowBase + r, colBase + c] <- byte value

                this.posCol <- this.posCol + 1

                if this.posCol >= cols then
                    this.NewLine ()

        member this.PutText(text: string) =
            for c in text do
                this.PutChar c

        // --- Export --------------------------------------------------------

        member this.ToPpm(scale: int) : byte[] =
            let w = width * scale
            let h = height * scale
            let header = sprintf "P6\n%d %d\n255\n" w h
            use ms = new IO.MemoryStream()
            let bytes = Encoding.ASCII.GetBytes header
            ms.Write(bytes, 0, bytes.Length)

            for y in 0 .. h - 1 do
                let sy = y / scale

                for x in 0 .. w - 1 do
                    let sx = x / scale
                    let r, gr, b = rgb (int pixels.[sy, sx])
                    ms.WriteByte r
                    ms.WriteByte gr
                    ms.WriteByte b

            ms.ToArray ()

        /// Render a half-height ASCII preview: each character shows the
        /// palette index (hex) of the sampled pixel, ' ' for paper colour.
        member this.ToAsciiArt(step: int) : string list =
            [ for y in 0 .. step .. height - 1 do
                  let line = System.Text.StringBuilder()

                  for x in 0 .. step .. width - 1 do
                      let p = int pixels.[y, x]

                      if p = this.posPaper then
                          line.Append ' ' |> ignore
                      else
                          line.Append(sprintf "%x" (p % 16)) |> ignore

                  yield line.ToString() ]