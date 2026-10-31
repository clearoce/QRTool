"""
生成 assets/qrtool.ico 和 assets/qrtool.png。

用途：修改二维码内容/配色后重新生成应用图标。
依赖：qrcode[pil]、Pillow
用法（在项目任意目录执行）：
    python local/scripts/make_icon.py
    python local/scripts/make_icon.py && dir assets
"""

from pathlib import Path

import qrcode
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets"
OUT.mkdir(exist_ok=True)

qr = qrcode.QRCode(
    error_correction=qrcode.constants.ERROR_CORRECT_M,
    box_size=7,
    border=3,
)
qr.add_data("QRTool")
qr.make(fit=True)
qr_image = qr.make_image(fill_color="#102a43", back_color="white").convert("RGBA")
qr_image = qr_image.resize((190, 190), Image.Resampling.NEAREST)

canvas = Image.new("RGBA", (256, 256), (37, 99, 235, 255))
draw = ImageDraw.Draw(canvas)
draw.rounded_rectangle((10, 10, 246, 246), radius=52, fill=(255, 255, 255, 255))
canvas.alpha_composite(qr_image, ((256 - 190) // 2, (256 - 190) // 2))

canvas.save(OUT / "qrtool.png")
canvas.save(
    OUT / "qrtool.ico",
    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
)
print("saved", (OUT / "qrtool.ico").resolve())
