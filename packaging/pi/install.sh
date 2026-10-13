#!/usr/bin/env bash
#
# BASIC Spectrum - Raspberry Pi (arm64) installer.
#
# Run on a freshly flashed Raspberry Pi OS Lite (64-bit, Bookworm or newer):
#
#     tar -xzf basic-pi-arm64.tar.gz
#     sudo ./install.sh [username]
#
# It installs the app to /opt/basic, adds a minimal X server + window manager,
# and makes the Pi boot straight into the BASIC shell on tty1.
#
set -euo pipefail

RAYLIB_VERSION="6.0"
RAYLIB_URL="https://github.com/raysan5/raylib/releases/download/${RAYLIB_VERSION}/raylib-${RAYLIB_VERSION}_linux_arm64.tar.gz"

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="/opt/basic"

log() { printf '\033[1;36m==>\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

[ "$(id -u)" -eq 0 ] || err "Please run as root:  sudo $0 [username]"

ARCH="$(uname -m)"
[ "$ARCH" = "aarch64" ] || err "This bundle is for arm64 (aarch64); this machine is $ARCH."

# Default to the user who invoked sudo, else the first normal account.
if [ "${1:-}" != "" ]; then
    TARGET_USER="$1"
elif [ "${SUDO_USER:-}" != "" ] && [ "$SUDO_USER" != "root" ]; then
    TARGET_USER="$SUDO_USER"
else
    TARGET_USER="$(awk -F: '$3>=1000 && $3<60000 {print $1; exit}' /etc/passwd)"
fi
id "$TARGET_USER" >/dev/null 2>&1 || err "Unknown user '$TARGET_USER'."
TARGET_HOME="$(getent passwd "$TARGET_USER" | cut -d: -f6)"
log "Installing for user '$TARGET_USER' ($TARGET_HOME)"

# --- 1. Packages ------------------------------------------------------------
log "Installing system packages (this may take a few minutes)"
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends \
    xserver-xorg xinit x11-xserver-utils xserver-xorg-legacy \
    matchbox-window-manager \
    libgl1-mesa-dri libglx-mesa0 libegl-mesa0 \
    libx11-6 libxrandr2 libxi6 libxcursor1 libxinerama1 libxext6 \
    alsa-utils ca-certificates curl

# ALSA userspace library: libasound2 (bookworm) or libasound2t64 (trixie+).
apt-get install -y --no-install-recommends libasound2 \
    || apt-get install -y --no-install-recommends libasound2t64 \
    || err "Could not install an ALSA library (libasound2 / libasound2t64)."

# --- 2. Application ---------------------------------------------------------
log "Installing application to $APP_DIR"
mkdir -p "$APP_DIR"
cp -a "$SRC_DIR/app/." "$APP_DIR/"

# Seed sample programs without clobbering anything the user already saved.
mkdir -p "$APP_DIR/programs"
if [ -d "$SRC_DIR/programs" ]; then
    for f in "$SRC_DIR"/programs/*.bas; do
        [ -e "$f" ] || continue
        dest="$APP_DIR/programs/$(basename "$f")"
        [ -e "$dest" ] || cp -a "$f" "$dest"
    done
fi

# Native raylib (bundled with the app; fetch if a slim bundle omitted it).
if [ ! -f "$APP_DIR/libraylib.so" ]; then
    log "libraylib.so missing - downloading raylib ${RAYLIB_VERSION} for arm64"
    tmp="$(mktemp -d)"
    curl -fsSL "$RAYLIB_URL" -o "$tmp/raylib.tgz" || err "Download failed (no internet?)"
    tar -xzf "$tmp/raylib.tgz" -C "$tmp"
    found="$(find "$tmp" -name 'libraylib.so*' -type f | head -n1)"
    [ -n "$found" ] || err "libraylib.so not found in archive"
    cp "$found" "$APP_DIR/libraylib.so"
    rm -rf "$tmp"
fi

chmod +x "$APP_DIR/basic"
chown -R "$TARGET_USER:$TARGET_USER" "$APP_DIR"

# --- 3. Graphics / audio groups --------------------------------------------
log "Adding '$TARGET_USER' to graphics/audio groups"
for grp in video render audio input plugdev; do
    getent group "$grp" >/dev/null && usermod -aG "$grp" "$TARGET_USER"
done

# --- 4. X server permissions ------------------------------------------------
log "Allowing startx for non-root users"
mkdir -p /etc/X11
cat >/etc/X11/Xwrapper.config <<'EOF'
allowed_users=anybody
needs_root_rights=yes
EOF

# --- 5. Console autologin on tty1 -------------------------------------------
log "Enabling console autologin for '$TARGET_USER' on tty1"
AGETTY="$(command -v agetty || echo /sbin/agetty)"
mkdir -p /etc/systemd/system/getty@tty1.service.d
cat >/etc/systemd/system/getty@tty1.service.d/autologin.conf <<EOF
[Service]
ExecStart=
ExecStart=-$AGETTY --autologin $TARGET_USER --noclear %I \$TERM
EOF

# --- 6. Start the shell on login --------------------------------------------
log "Wiring up the X session"
cat >"$TARGET_HOME/.xinitrc" <<'EOF'
#!/bin/sh
# Keep the screen awake.
xset s off
xset -dpms
xset s noblank

# Tiny window manager so the app gets keyboard focus.
matchbox-window-manager -use_titlebar no &

# Give the server a moment, then run the BASIC shell.
sleep 1
exec /opt/basic/basic
EOF
chmod +x "$TARGET_HOME/.xinitrc"
chown "$TARGET_USER:$TARGET_USER" "$TARGET_HOME/.xinitrc"

BASH_PROFILE="$TARGET_HOME/.bash_profile"
MARKER="# --- BASIC Spectrum autostart ---"
if ! grep -qF "$MARKER" "$BASH_PROFILE" 2>/dev/null; then
    cat >>"$BASH_PROFILE" <<'EOF'

# --- BASIC Spectrum autostart ---
# On the local console (tty1), launch the graphical BASIC shell.
if [ -z "${DISPLAY:-}" ] && [ "$(tty)" = "/dev/tty1" ]; then
    exec startx
fi
EOF
    chown "$TARGET_USER:$TARGET_USER" "$BASH_PROFILE"
fi

# --- 7. Boot config: ensure KMS graphics + onboard audio --------------------
CONFIG_TXT="/boot/firmware/config.txt"
[ -f "$CONFIG_TXT" ] || CONFIG_TXT="/boot/config.txt"
if [ -f "$CONFIG_TXT" ]; then
    log "Tuning $CONFIG_TXT"
    grep -q '^dtparam=audio=on' "$CONFIG_TXT" || echo 'dtparam=audio=on' >>"$CONFIG_TXT"
    grep -q '^dtoverlay=vc4-kms-v3d' "$CONFIG_TXT" || echo 'dtoverlay=vc4-kms-v3d' >>"$CONFIG_TXT"
fi

log "Done!"
cat <<EOF

The BASIC Spectrum shell is installed. Reboot to start it automatically:

    sudo reboot

The Pi will log in on tty1 and launch BASIC full-screen. Insert a USB
keyboard; type programs with line numbers (e.g. 10 PRINT "HI") then RUN.

App:      $APP_DIR
Programs: $APP_DIR/programs
User:     $TARGET_USER
EOF
