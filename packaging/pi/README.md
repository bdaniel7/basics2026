# BASIC Spectrum on Raspberry Pi (arm64)

This bundles the `Basic.Pi` graphical shell as a self-contained .NET app for
64-bit Raspberry Pi OS (Pi 4/5/400/CM4/Zero 2 W), turns on a minimal X server,
and makes the Pi boot straight into the BASIC shell on a TV or monitor.

## 1. Build the bundle (on Windows)

```powershell
pwsh -File packaging/pi/build-on-windows.ps1
```

This cross-publishes `Basic.Pi` for `linux-arm64`, drops in the native
**raylib 6.0** library for arm64 (Raylib-cs ships none for this architecture),
copies the sample programs and the installer, and writes:

```
dist/basic-pi-arm64.tar.gz
```

## 2. Flash Raspberry Pi OS

Use Raspberry Pi Imager to flash **Raspberry Pi OS Lite (64-bit)** (Bookworm or
newer) to an SD card. In the Imager settings, set a username/password and
enable SSH if you like.

## 3. Copy and install

Boot the Pi, copy the tarball over (scp, USB stick, ...), then:

```bash
tar -xzf basic-pi-arm64.tar.gz
sudo bash install.sh          # add a username to override the target user
sudo reboot
```

After rebooting, the Pi logs in automatically on tty1 and launches the BASIC
shell full-screen. Plug in a USB keyboard and start typing.

## Controlling the shell

| Mode    | Keys |
|---------|------|
| Menu    | `1` type a program, `2` load, `3` help, `4` quit |
| Editor  | type `10 PRINT "HI"`, then `RUN`; `LIST`, `NEW`, `SAVE name`, `LOAD`; `ESC` back to menu |
| Running | type + `ENTER` to answer `INPUT`; `SPACE` returns after a program finishes; `ESC` stops |

## Files installed on the Pi

- `/opt/basic` - the app (`basic`, `libraylib.so`, runtime)
- `/opt/basic/programs` - saved `.bas` programs (seeded with examples)
- `~/.xinitrc` - starts the window manager and the app
- `~/.bash_profile` - launches `startx` on tty1
- `/etc/X11/Xwrapper.config` - allows non-root X
- `/etc/systemd/system/getty@tty1.service.d/autologin.conf` - console autologin

## Notes

- Requires internet **only during install** (to fetch packages and, if needed,
  raylib). The finished device works fully offline.
- The window scales to the monitor automatically (fits 720p and 1080p).
- To exit to the console, press `ESC` in the menu, or switch tty with
  `Ctrl+Alt+F2`.
