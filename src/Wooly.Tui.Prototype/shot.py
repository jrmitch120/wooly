"""Run the prototype in a real pty of a fixed size, answer its terminal queries, and print the screen as text."""
import os, pty, sys, select, fcntl, termios, struct, re, time

variant = sys.argv[1]
cols = int(sys.argv[2]) if len(sys.argv) > 2 else 100
rows = int(sys.argv[3]) if len(sys.argv) > 3 else 32
keys = sys.argv[4] if len(sys.argv) > 4 else ""
# With keys, the app is killed mid-frame rather than stopped: a graceful exit erases any modal it opened.
hold = "999999" if keys else "700"
dll = "src/Wooly.Tui.Prototype/bin/Debug/net10.0/Wooly.Tui.Prototype.dll"

master, slave = pty.openpty()

# Echo off before the child ever sees the pty, so our answers to its terminal queries do not come back as text.
mode = termios.tcgetattr(slave)
mode[3] &= ~termios.ECHO
termios.tcsetattr(slave, termios.TCSANOW, mode)
fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", rows, cols, 0, 0))

pid = os.fork()
if pid == 0:
    os.close(master)
    os.setsid()
    fcntl.ioctl(slave, termios.TIOCSCTTY, 0)
    os.dup2(slave, 0)
    os.dup2(slave, 1)
    os.dup2(slave, 2)
    os.close(slave)
    os.environ["TERM"] = "xterm-256color"
    os.environ["COLUMNS"] = str(cols)
    os.environ["LINES"] = str(rows)
    argv = ["dotnet", dll, "--shot", variant, "--hold", hold]
    if keys:
        argv += ["--keys", keys]
    os.execvp("dotnet", argv)

os.close(slave)
fd = master

out = b""
started = time.time()
deadline = started + (3.0 if keys else 20)
while time.time() < deadline:
    r, _, _ = select.select([fd], [], [], 0.3)
    if not r:
        continue
    try:
        chunk = os.read(fd, 65536)
    except OSError:
        break
    if not chunk:
        break
    out += chunk
    # Answer the queries a real terminal emulator would answer.
    if b"\x1b[18t" in chunk:
        os.write(fd, f"\x1b[8;{rows};{cols}t".encode())
    if b"\x1b[6n" in chunk:
        os.write(fd, f"\x1b[{rows};{cols}R".encode())
    if b"\x1b[0c" in chunk:
        os.write(fd, b"\x1b[?1;2c")
    if b"\x1b]10;?" in chunk:
        os.write(fd, b"\x1b]10;rgb:ffff/ffff/ffff\x1b\\")
    if b"\x1b[?u" in chunk:
        os.write(fd, b"\x1b[?0u")

if keys:
    import signal
    try:
        os.kill(pid, signal.SIGKILL)
    except ProcessLookupError:
        pass

os.close(fd)
try:
    os.waitpid(pid, 0)
except ChildProcessError:
    pass

# Replay the output onto a grid: absolute cursor moves, erases, and text. Colours are dropped.
grid = [[" "] * cols for _ in range(rows)]
cy = cx = 0
i = 0
text = out.decode("utf-8", "replace")
while i < len(text):
    ch = text[i]
    if ch == "\x1b":
        m = re.match(r"\x1b\[([\x30-\x3f]*)([\x20-\x2f]*)([\x40-\x7e])", text[i:])
        if m:
            args, cmd = m.group(1), m.group(3)
            nums = [int(n) for n in args.split(";") if n.isdigit()]
            if cmd == "H":
                cy = (nums[0] - 1) if len(nums) > 0 else 0
                cx = (nums[1] - 1) if len(nums) > 1 else 0
            elif cmd == "J":
                if nums and nums[0] == 2:
                    grid = [[" "] * cols for _ in range(rows)]
            elif cmd == "K":
                if 0 <= cy < rows:
                    for x in range(cx, cols):
                        grid[cy][x] = " "
            elif cmd in "ABCD":
                n = nums[0] if nums else 1
                cy += n if cmd == "B" else -n if cmd == "A" else 0
                cx += n if cmd == "C" else -n if cmd == "D" else 0
            i += m.end()
            continue
        m = re.match(r"\x1b\][^\x07\x1b]*(\x07|\x1b\\)", text[i:])
        if m:
            i += m.end()
            continue
        m = re.match(r"\x1b[=>]|\x1b\(.|\x1b\).", text[i:])
        if m:
            i += m.end()
            continue
        i += 1
        continue
    if ch == "\n":
        cy, cx, i = cy + 1, 0, i + 1
        continue
    if ch == "\r":
        cx, i = 0, i + 1
        continue
    if ch == "\x08":
        cx, i = max(0, cx - 1), i + 1
        continue
    if ord(ch) < 32:
        i += 1
        continue
    if 0 <= cy < rows and 0 <= cx < cols:
        grid[cy][cx] = ch
    cx += 1
    i += 1

for row in grid:
    print("".join(row).rstrip())
