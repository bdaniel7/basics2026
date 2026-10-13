namespace Basic.Pi

open Raylib_cs
open Basic.Core

/// Turns the BASIC screen's indexed pixels into raylib colours.
module Renderer =

    let colour (index: int) : Color =
        let r, g, b = Screen.rgb index
        Color(r, g, b, 255uy)

    /// Convert the whole framebuffer into a Colour array (row-major),
    /// ready for Raylib.UpdateTexture.
    let toColours (screen: Screen.Screen) : Color[] =
        let arr = Array.zeroCreate<Color> (Screen.width * Screen.height)

        for y in 0 .. Screen.height - 1 do
            for x in 0 .. Screen.width - 1 do
                arr.[y * Screen.width + x] <- colour (int screen.Pixels.[y, x])

        arr