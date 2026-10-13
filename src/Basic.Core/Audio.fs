namespace Basic.Core

open System
open System.Text

module Audio =

    /// One scheduled tone on the track.
    type private Tone =
        { Start: float
          Freq: float
          Dur: float
          Vol: float }

    /// A simple sequential tone track.
    ///
    /// Beeps are queued back-to-back (a kid-friendly, blocking model): each
    /// BEEP/SOUND plays in full before the next starts, so the whole program
    /// can be rendered into a single WAV afterwards.
    type Audio() =
        let sampleRate = 44100
        let tones = ResizeArray<Tone>()
        let played = Event<float * float>()
        let mutable cursor = 0.0

        member val Vol = 0.8 with get, set

        member _.SampleRate = sampleRate

        /// Raised (frequency Hz, duration seconds) whenever a tone is played,
        /// so a live front-end can stream it in real time.
        member _.Played = played.Publish

        /// Sinclair BEEP: duration in seconds, pitch in semitones above middle C.
        member this.Beep(duration: float, pitch: float) =
            let freq = 261.63 * (2.0 ** (pitch / 12.0))
            this.Play(freq, duration)

        /// Extension: absolute frequency in Hz, duration in seconds.
        member this.Play(freq: float, duration: float) =
            let d = max 0.0 duration
            let f = max 20.0 freq

            if d > 0.0 then
                tones.Add { Start = cursor; Freq = f; Dur = d; Vol = this.Vol }
                played.Trigger(f, d)

            cursor <- cursor + d

        /// Total rendered length in seconds.
        member _.Duration = cursor

        member _.ToneCount = tones.Count

        /// Render the track to a 16-bit mono PCM WAV file.
        member this.ToWav() : byte[] =
            let totalSamples = int (Math.Ceiling(cursor * float sampleRate))
            let samples = Array.zeroCreate<int16> totalSamples

            for tone in tones do
                let startIdx = int (tone.Start * float sampleRate)
                let count = int (tone.Dur * float sampleRate)

                for i in 0 .. count - 1 do
                    let idx = startIdx + i

                    if idx < totalSamples then
                        let t = float i / float sampleRate
                        let attack = Math.Min(1.0, t / 0.008)
                        let release = Math.Min(1.0, (tone.Dur - t) / 0.008)
                        let env = attack * release
                        let value =
                            Math.Sin(2.0 * Math.PI * tone.Freq * t)
                            * tone.Vol
                            * env
                            * 12000.0

                        let combined = float samples.[idx] + value
                        let clamped = Math.Max(-32767.0, Math.Min(32767.0, combined))
                        samples.[idx] <- int16 (Math.Round clamped)

            let dataBytes = samples.Length * 2
            let wav = Array.zeroCreate<byte> (44 + dataBytes)

            let ascii (offset: int) (text: string) =
                let b = Encoding.ASCII.GetBytes text
                Array.Copy(b, 0, wav, offset, b.Length)

            let u32 (offset: int) (v: uint32) =
                BitConverter.GetBytes(v).CopyTo(wav, offset)

            let u16 (offset: int) (v: uint16) =
                BitConverter.GetBytes(v).CopyTo(wav, offset)

            ascii 0 "RIFF"
            u32 4 (uint32 (36 + dataBytes))
            ascii 8 "WAVE"
            ascii 12 "fmt "
            u32 16 16u
            u16 20 1us
            u16 22 1us
            u32 24 (uint32 sampleRate)
            u32 28 (uint32 (sampleRate * 2))
            u16 32 2us
            u16 34 16us
            ascii 36 "data"
            u32 40 (uint32 dataBytes)

            for i in 0 .. samples.Length - 1 do
                BitConverter.GetBytes(samples.[i]).CopyTo(wav, 44 + i * 2)

            wav
