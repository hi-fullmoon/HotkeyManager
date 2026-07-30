"""生成 HotkeyManager 的应用图标：深色圆角键帽 + 黄色闪电。

输出：
  src/HotkeyManager/Assets/app.ico  多尺寸（16/24/32/48/64/128/256）
  assets/icon-preview.png           256px 预览图
"""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
ICO_PATH = ROOT / "src" / "HotkeyManager" / "Assets" / "app.ico"
PREVIEW_PATH = ROOT / "assets" / "icon-preview.png"

SIZE = 256          # 基准画布，小尺寸由 Pillow 高质量缩放
SS = 4              # 超采样倍数，抗锯齿

BG = (30, 41, 59, 255)        # 深炭灰 #1E293B
BG_LIGHT = (71, 85, 105, 255)  # 顶部亮灰 #475569
BOLT = (250, 204, 21, 255)    # 明黄 #FACC15
BOLT_EDGE = (202, 138, 4, 255)  # 闪电描边 #CA8A04


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def draw_icon() -> Image.Image:
    s = SIZE * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def pt(x, y):
        return (x * SS, y * SS)

    # 键帽：圆角方块 + 自上而下的柔和渐变（浅靛蓝 → 深靛蓝）
    top, bottom = pt(8, 8), pt(248, 248)
    for y in range(top[1], bottom[1]):
        t = (y - top[1]) / (bottom[1] - top[1])
        d.line([(top[0], y), (bottom[0], y)], fill=lerp(BG_LIGHT, BG, t))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle(top + bottom, radius=52 * SS, fill=255)
    img.putalpha(mask)

    d = ImageDraw.Draw(img)

    # 闪电（经典 zigzag 形状，基准坐标基于 256）
    bolt = [
        (150, 30),
        (82, 142),
        (122, 142),
        (102, 226),
        (176, 106),
        (132, 106),
    ]
    d.polygon([pt(x, y) for x, y in bolt], fill=BOLT, outline=BOLT_EDGE, width=4 * SS)

    return img.resize((SIZE, SIZE), Image.LANCZOS)


def main() -> None:
    icon = draw_icon()
    ICO_PATH.parent.mkdir(parents=True, exist_ok=True)
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)

    icon.save(
        ICO_PATH,
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    icon.save(PREVIEW_PATH)
    print(f"written: {ICO_PATH}")
    print(f"written: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
