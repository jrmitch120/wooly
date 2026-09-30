# PROTOTYPE — throwaway. Turns an IDriver.ToAnsi() frame into a PNG so a skin can be looked at without a terminal.
import re, sys
from PIL import Image, ImageDraw, ImageFont

src, dst = sys.argv[1], sys.argv[2]
text = re.sub(r"\x1b\][^\x1b\x07]*(\x1b\\|\x07)", "", open(src, encoding="utf-8").read())
font = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 15, index=0)
bold = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 15, index=1)
cw, ch = 9, 19
lines = text.split("\n")
if lines and lines[-1] == "":
    lines.pop()
cols = max(len(re.sub(r"\x1b\[[0-9;]*[A-Za-z]", "", l)) for l in lines)
img = Image.new("RGB", (cols * cw, len(lines) * ch), (0, 0, 0))
d = ImageDraw.Draw(img)
fg, bg, isbold = (200, 200, 200), (0, 0, 0), False
for y, line in enumerate(lines):
    x = 0
    for tok in re.split(r"(\x1b\[[0-9;]*m)", line):
        if tok.startswith("\x1b["):
            p = [int(v) if v else 0 for v in tok[2:-1].split(";")]
            i = 0
            while i < len(p):
                if p[i] == 38 and p[i+1] == 2: fg = tuple(p[i+2:i+5]); i += 5
                elif p[i] == 48 and p[i+1] == 2: bg = tuple(p[i+2:i+5]); i += 5
                elif p[i] == 0: fg, bg, isbold = (200,200,200), (0,0,0), False; i += 1
                elif p[i] == 1: isbold = True; i += 1
                elif p[i] == 22: isbold = False; i += 1
                else: i += 1
            continue
        for c in tok:
            if c == "\r": continue
            d.rectangle([x*cw, y*ch, (x+1)*cw-1, (y+1)*ch-1], fill=bg)
            if c != " ":
                d.text((x*cw, y*ch + 1), c, font=bold if isbold and ord(c) < 0x2000 else font, fill=fg)
            x += 1
img.save(dst)
