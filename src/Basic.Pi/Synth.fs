namespace Basic.Pi

open System
open System.Collections.Generic

/// A real-time monophonic tone queue feeding raylib's audio stream.
/// Tones play back-to-back, mirroring the interpreter's sequential model.
type Synth(sampleRate: int) =
    let queue = Queue<float * float>()
    let mutable curFreq = 0.0
    let mutable curTotal = 0
    let mutable curPos = 0
    let mutable remaining = 0

    member _.Enqueue(freq: float, duration: float) =
        if duration > 0.0 then
            queue.Enqueue(freq, duration)

    member _.Pending = queue.Count

    member _.Clear() =
        queue.Clear()
        remaining <- 0
        curTotal <- 0
        curPos <- 0

    /// Fill a mono buffer with float samples in [-1, 1].
    member _.Fill(buffer: float32[]) =
        for i in 0 .. buffer.Length - 1 do
            if remaining = 0 && queue.Count > 0 then
                let freq, dur = queue.Dequeue()
                curFreq <- freq
                curTotal <- max 1 (int (dur * float sampleRate))
                curPos <- 0
                remaining <- curTotal

            if remaining > 0 then
                let t = float curPos / float sampleRate
                let total = float curTotal / float sampleRate
                let attack = Math.Min(1.0, t / 0.008)
                let release = Math.Min(1.0, (total - t) / 0.008)
                let env = attack * release
                buffer.[i] <- float32 (Math.Sin(2.0 * Math.PI * curFreq * t) * env * 0.35)
                curPos <- curPos + 1
                remaining <- remaining - 1
            else
                buffer.[i] <- 0.0f