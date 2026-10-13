module Basic.Pi.Program

[<EntryPoint>]
let main _argv =
    let app = Basic.Pi.App()
    app.Run()
    0