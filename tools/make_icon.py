# -*- coding: utf-8 -*-
"""生成 MabaoClone 的图标（16/32/48/64/128/256 多尺寸 ico）。"""
from PIL import Image, ImageDraw
import os

out = r"D:\ds\mabao-clone\src\icon.ico"
sizes = [16, 24, 32, 48, 64, 128, 256]


def make(size):
    s = size * 8  # 超采样
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 圆角底板
    r = int(s * 0.22)
    d.rounded_rectangle([int(s * 0.02), int(s * 0.02), int(s * 0.98), int(s * 0.98)],
                        radius=r, fill=(28, 30, 38, 255), outline=(90, 170, 255, 255),
                        width=max(2, int(s * 0.035)))
    # 播放三角
    cx, cy = s * 0.52, s * 0.5
    t = s * 0.26
    d.polygon([(cx - t * 0.75, cy - t), (cx - t * 0.75, cy + t), (cx + t, cy)],
              fill=(120, 200, 255, 255))
    # 顶部“置顶”横条
    d.rounded_rectangle([int(s * 0.22), int(s * 0.15), int(s * 0.78), int(s * 0.24)],
                        radius=int(s * 0.03), fill=(255, 190, 80, 255))
    return im.resize((size, size), Image.LANCZOS)


imgs = [make(x) for x in sizes]
imgs[0].save(out, format="ICO", sizes=[(x, x) for x in sizes], append_images=imgs[1:])
print("written:", out, os.path.getsize(out), "bytes")
