namespace Basic.Pi

open System
open System.Collections.Generic

type SubmitResult =
    /// A line-numbered program line was entered / replaced.
    | SetLine of int
    /// A direct command such as RUN, LIST, NEW, LOAD, SAVE, HELP.
    | Command of string * string list
    | Nothing

/// The line-numbered program editor, like a Spectrum's bottom screen area.
type Editor() =
    let lines = SortedDictionary<int, string>()

    member _.Lines = lines |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList

    member _.Count = lines.Count

    member _.ProgramText =
        lines
        |> Seq.map (fun kv -> sprintf "%d %s" kv.Key kv.Value)
        |> String.concat "\n"

    member _.Clear() = lines.Clear()

    member _.Remove(n: int) = lines.Remove(n) |> ignore

    member _.SetLine(n: int, text: string) =
        if text.Trim() = "" then
            lines.Remove(n) |> ignore
        else
            lines.[n] <- text.Trim()

    /// Interpret a line of input the child has typed.
    member this.Submit(input: string) : SubmitResult =
        let t = input.Trim()

        if t = "" then
            Nothing
        else
            let idx = t.IndexOf ' '
            let first = if idx < 0 then t else t.Substring(0, idx)

            match Int32.TryParse first with
            | true, n ->
                let body = if idx < 0 then "" else t.Substring(idx + 1)
                this.SetLine(n, body)
                SetLine n
            | _ ->
                let parts =
                    t.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> Array.toList

                match parts with
                | head :: rest -> Command(head.ToUpperInvariant(), rest)
                | [] -> Nothing

    /// Load a saved program from its textual form.
    member this.LoadText(text: string) =
        lines.Clear()

        for raw in text.Split('\n') do
            let t = raw.TrimEnd('\r').Trim()

            if t <> "" then
                let idx = t.IndexOf ' '
                let first = if idx < 0 then t else t.Substring(0, idx)

                match Int32.TryParse first with
                | true, n ->
                    lines.[n] <- (if idx < 0 then "" else t.Substring(idx + 1).Trim())
                | _ -> ()