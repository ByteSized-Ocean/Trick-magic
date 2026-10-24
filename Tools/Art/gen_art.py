#!/usr/bin/env python3
"""《障眼法》剪影素材生成器（GDD §19.4：脚本生成，不手绘）。

用法：python3 Tools/Art/gen_art.py
输出：Assets/Art/Sprites/*.png，文件名与尺寸见 GDD §19。改配色或造型后重跑即可。

画法：先按 4 倍尺寸画矢量剪影，再缩到目标尺寸；剪影统一近黑色，
朝光一侧描 1px 暖棕边，最后叠一层丝网印刷颗粒。黄色只属于追光（标题卡里那束光除外）。
"""
import math
import os
import random
import zlib

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.environ.get("SLEIGHT_ART_OUT") or os.path.join(ROOT, "Assets", "Art", "Sprites")

# 调色板（团队定稿，取代 GDD §5.1 的灰色系）
NEAR_BLACK = (14, 11, 10)
WARM_BROWN = (74, 52, 38)
AGED_PAPER = (217, 203, 176)
CRIMSON = (90, 20, 24)
SODIUM_YELLOW = (255, 210, 30)

S = 4  # 超采样倍数


class Canvas:
    """y 轴朝下、以目标像素为单位的画布，内部按 S 倍绘制。"""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.mask = Image.new("L", (w * S, h * S), 0)
        self.d = ImageDraw.Draw(self.mask)
        self.details = []  # (颜色, 蒙版)：画在剪影之上的点缀

    def _p(self, pts):
        return [(x * S, y * S) for x, y in pts]

    def poly(self, pts, fill=255):
        self.d.polygon(self._p(pts), fill=fill)

    def ellipse(self, box, fill=255):
        x0, y0, x1, y1 = box
        self.d.ellipse((x0 * S, y0 * S, x1 * S, y1 * S), fill=fill)

    def rect(self, box, fill=255):
        x0, y0, x1, y1 = box
        self.d.rectangle((x0 * S, y0 * S, x1 * S, y1 * S), fill=fill)

    def limb(self, pts, width, fill=255):
        p = self._p(pts)
        self.d.line(p, fill=fill, width=int(width * S), joint="curve")
        r = width * S / 2
        for x, y in (p[0], p[-1]):
            self.d.ellipse((x - r, y - r, x + r, y + r), fill=fill)

    def detail(self, color):
        """返回一个点缀层的绘图对象，内容画成 color。"""
        m = Image.new("L", self.mask.size, 0)
        self.details.append((color, m))
        return DetailLayer(m)

    def render(self, rim=True, grain=6, seed=0):
        mask = self.mask.resize((self.w, self.h), Image.LANCZOS)
        img = Image.new("RGBA", (self.w, self.h), NEAR_BLACK + (0,))
        img.putalpha(mask)

        if rim:
            # 光从右上来：剪影右、上边缘描 1px 暖棕
            hard = mask.point(lambda a: 255 if a > 128 else 0)
            shifted = ImageChops.offset(hard, -1, 1)
            edge = ImageChops.subtract(hard, shifted)
            brown = Image.new("RGBA", img.size, WARM_BROWN + (255,))
            img.paste(brown, (0, 0), edge)
            img.putalpha(mask)

        for color, m in self.details:
            dm = m.resize((self.w, self.h), Image.LANCZOS)
            layer = Image.new("RGBA", img.size, color + (255,))
            img.paste(layer, (0, 0), dm)
            img.putalpha(ImageChops.lighter(img.getchannel("A"), dm))

        return add_grain(img, grain, seed)


class DetailLayer:
    def __init__(self, m):
        self.d = ImageDraw.Draw(m)

    def line(self, pts, width=1):
        self.d.line([(x * S, y * S) for x, y in pts], fill=255, width=max(1, int(width * S)))

    def poly(self, pts):
        self.d.polygon([(x * S, y * S) for x, y in pts], fill=255)

    def ellipse(self, box):
        x0, y0, x1, y1 = box
        self.d.ellipse((x0 * S, y0 * S, x1 * S, y1 * S), fill=255)

    def rect(self, box):
        x0, y0, x1, y1 = box
        self.d.rectangle((x0 * S, y0 * S, x1 * S, y1 * S), fill=255)


def add_grain(img, amount, seed):
    """丝网印刷颗粒：只改颜色不改 alpha。"""
    if amount <= 0:
        return img
    rnd = random.Random(seed)
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            n = rnd.randint(-amount, amount)
            px[x, y] = (clamp(r + n), clamp(g + n), clamp(b + n), a)
    return img


def clamp(v):
    return max(0, min(255, v))


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name, img.size)


def rot(cx, cy, pts, ang):
    c, s = math.cos(ang), math.sin(ang)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def polar(origin, length, ang):
    """ang = 0 指向正下方，正值朝右（角色面朝右）。"""
    return (origin[0] + math.sin(ang) * length, origin[1] + math.cos(ang) * length)


# ---------------------------------------------------------------- 魔术师
# 128×192，脚底贴画布最下一行（§19.1）。瘦、疲惫、破燕尾服、过高的高礼帽、三根手指、弯掉的廉价魔杖。

def magician(pose):
    c = Canvas(128, 192)
    d = math.radians
    hip = (62, 112)
    bow = d(pose.get("bow", 0))
    slump = d(6)  # 驼背
    torso_ang = bow + slump
    neck = (hip[0] + math.sin(torso_ang) * 50, hip[1] - math.cos(torso_ang) * 50 + pose.get("breath", 0))
    up = (math.sin(torso_ang), -math.cos(torso_ang))
    side = (math.cos(torso_ang), math.sin(torso_ang))

    # 腿：大腿 + 小腿 + 鞋
    for leg in ("back", "front"):
        thigh, knee = pose["legs"][leg]
        k = polar(hip, 38, d(thigh))
        a = polar(k, 38, d(thigh - knee))
        a = (a[0], min(a[1], 186))
        c.limb([hip, k, a], 7)
        c.limb([a, (a[0] + 9, a[1] + 1)], 5)

    # 燕尾服：肩宽腰窄，后摆两条燕尾
    sh_l = (neck[0] - side[0] * 15, neck[1] - side[1] * 15)
    sh_r = (neck[0] + side[0] * 13, neck[1] + side[1] * 13)
    w_l = (hip[0] - 10, hip[1])
    w_r = (hip[0] + 9, hip[1])
    c.poly([sh_l, sh_r, w_r, w_l])
    tail = d(pose.get("tail", 0))
    t1 = polar(w_l, 34, d(12) + tail)
    t2 = polar((w_l[0] + 6, w_l[1]), 30, d(4) + tail)
    c.poly([w_l, (w_l[0] + 10, w_l[1] - 4), t2, (t1[0] + 3, t1[1]), t1])

    # 脖子与头
    head = (neck[0] + up[0] * 11, neck[1] + up[1] * 11)
    c.limb([neck, head], 6)
    c.ellipse((head[0] - 9, head[1] - 10, head[0] + 10, head[1] + 9))
    # 鼻子（侧脸朝右）
    c.poly([(head[0] + 8, head[1] - 2), (head[0] + 14, head[1] + 2), (head[0] + 8, head[1] + 4)])

    # 过高的高礼帽：帽檐 + 帽筒，帽筒略微歪
    hat_ang = torso_ang + d(pose.get("hat", -4))
    hb = (head[0] + up[0] * 7, head[1] + up[1] * 7)
    brim = [(hb[0] - 15, hb[1] - 2), (hb[0] + 16, hb[1] - 2), (hb[0] + 16, hb[1] + 2), (hb[0] - 15, hb[1] + 2)]
    crown = [(hb[0] - 10, hb[1] - 1), (hb[0] + 11, hb[1] - 1), (hb[0] + 13, hb[1] - 44), (hb[0] - 9, hb[1] - 44)]
    c.poly(rot(hb[0], hb[1], brim, hat_ang))
    c.poly(rot(hb[0], hb[1], crown, hat_ang))
    band = c.detail(WARM_BROWN)
    band.poly(rot(hb[0], hb[1], [(hb[0] - 10, hb[1] - 2), (hb[0] + 11, hb[1] - 2), (hb[0] + 11, hb[1] - 6), (hb[0] - 10, hb[1] - 6)], hat_ang))

    # 手臂：上臂 + 前臂 + 三根手指；前手拿弯掉的魔杖
    for arm in ("back", "front"):
        upper, fore = pose["arms"][arm]
        sh = sh_l if arm == "back" else sh_r
        e = polar(sh, 26, d(upper) + torso_ang)
        h = polar(e, 24, d(upper + fore) + torso_ang)
        c.limb([sh, e, h], 5)
        hand_ang = d(upper + fore) + torso_ang
        for spread in (-28, 0, 28):
            f = polar(h, 6, hand_ang + d(spread))
            c.limb([h, f], 1.8)
        if arm == "front" and pose.get("wand", True):
            wa = hand_ang + d(pose.get("wand_ang", -70))
            mid = polar(h, 13, wa)
            tip = polar(mid, 11, wa + d(28))  # 折了一下
            c.limb([h, mid, tip], 2.2)
            chip = c.detail(AGED_PAPER)
            chip.line([polar(mid, 7, wa + d(28)), tip], 2)  # 杖头褪色的白漆

    # 燕尾服上的旧纸色衬衫领口
    collar = c.detail(AGED_PAPER)
    collar.poly([(neck[0] - 1, neck[1] + 2), (neck[0] + 6, neck[1] + 2), (neck[0] + 3, neck[1] + 12)])
    return c.render(seed=zlib.crc32(repr(sorted(pose.items())).encode()))


STAND_LEGS = {"back": (-4, 2), "front": (4, 2)}
IDLE_ARMS = {"back": (8, 6), "front": (-14, -50)}

MAGICIAN = {
    "player_idle_00": {"legs": STAND_LEGS, "arms": IDLE_ARMS},
    "player_idle_01": {"legs": STAND_LEGS, "arms": {"back": (8, 8), "front": (-12, -52)}, "breath": 1.5},
    "player_walk_00": {"legs": {"back": (-20, 6), "front": (20, 4)}, "arms": {"back": (-18, -8), "front": (12, -40)}, "tail": -4},
    "player_walk_01": {"legs": {"back": (-6, 28), "front": (6, 2)}, "arms": {"back": (-4, -6), "front": (2, -46)}, "breath": 1},
    "player_walk_02": {"legs": {"back": (20, 4), "front": (-20, 6)}, "arms": {"back": (14, -4), "front": (-16, -44)}, "tail": -4},
    "player_walk_03": {"legs": {"back": (6, 2), "front": (-6, 28)}, "arms": {"back": (4, -6), "front": (-4, -48)}, "breath": 1},
    "player_act_00": {"legs": {"back": (-8, 10), "front": (14, 18)}, "arms": {"back": (10, 4), "front": (-70, -10)}, "bow": 18, "wand": False},
    "player_act_01": {"legs": {"back": (-6, 6), "front": (8, 8)}, "arms": {"back": (8, 6), "front": (-40, -30)}, "bow": 8, "wand": False},
    "player_perform_00": {"legs": STAND_LEGS, "arms": {"back": (10, 10), "front": (-150, -10)}, "wand_ang": -30},
    "player_perform_01": {"legs": {"back": (-10, 4), "front": (10, 6)}, "arms": {"back": (-60, -70), "front": (-80, 30)}, "bow": 38, "hat": -14, "tail": 10},
    "player_perform_02": {"legs": STAND_LEGS, "arms": {"back": (8, 6), "front": (-40, -60)}, "bow": 10},
    "player_caught_00": {"legs": {"back": (-8, 4), "front": (8, 4)}, "arms": {"back": (-120, -30), "front": (-130, 30)}, "bow": -8, "hat": 12, "wand_ang": 40},
}


# ---------------------------------------------------------------- 道具（§19.2，均贴地面线）

def plank_lines(c, x0, x1, ys):
    wood = c.detail(WARM_BROWN)
    for y in ys:
        wood.line([(x0, y), (x1, y)], 1)


def box_open():
    c = Canvas(48, 56)
    c.rect((3, 20, 45, 56))                       # 箱身（矮）
    c.poly([(3, 20), (6, 2), (12, 2), (9, 20)])   # 竖起来的箱盖
    plank_lines(c, 5, 43, [30, 40, 50])
    inside = c.detail(AGED_PAPER)                 # 里面露出的东西：一角旧手帕
    inside.poly([(18, 21), (24, 11), (29, 15), (33, 21)])
    return c.render(seed=11)


def box_sealed():
    c = Canvas(48, 44)
    c.rect((3, 8, 45, 44))
    c.rect((1, 5, 47, 10))                        # 合上的盖沿
    plank_lines(c, 5, 43, [20, 30, 39])
    latch = c.detail(AGED_PAPER)
    latch.rect((22, 9, 26, 14))
    return c.render(seed=12)


def cloth_off():
    c = Canvas(72, 40)
    c.ellipse((18, 8, 50, 40))                    # 底下那一团（鸟笼似的）
    c.rect((18, 24, 50, 40))
    bars = c.detail(WARM_BROWN)
    for x in (24, 30, 36, 42):
        bars.line([(x, 12), (x, 39)], 1)
    c.poly([(48, 40), (54, 30), (62, 33), (70, 40)])  # 掀到一边的布
    return c.render(seed=13)


def cloth_on():
    c = Canvas(72, 56)
    c.ellipse((6, 4, 66, 64))                     # 圆顶蓬
    c.rect((6, 34, 66, 56))
    c.poly([(4, 56), (8, 46), (14, 56)])
    c.poly([(58, 56), (64, 46), (68, 56)])
    folds = c.detail(WARM_BROWN)
    for x0, x1 in ((22, 16), (36, 34), (50, 56)):
        folds.line([(x0, 12), (x1, 54)], 1)
    tassel = c.detail(CRIMSON)
    for x in range(10, 64, 9):
        tassel.line([(x, 54), (x, 56)], 2)
    return c.render(seed=14)


def mirror(flipped):
    c = Canvas(40, 112)
    c.rect((6, 4, 34, 92))                        # 镜框
    c.ellipse((4, 0, 36, 12))
    c.limb([(20, 92), (20, 104)], 4)              # 立柱
    c.poly([(6, 112), (20, 102), (34, 112)])      # 三脚底座
    if flipped:
        # 镜面朝外：照不出任何人，只有一道旧纸色的亮边和一抹斜光
        edge = c.detail(AGED_PAPER)
        edge.rect((29, 10, 31, 86))
        sheen = c.detail(WARM_BROWN)
        sheen.line([(11, 60), (25, 24)], 2)
    else:
        back = c.detail(WARM_BROWN)               # 背板的木条
        back.line([(10, 20), (30, 20)], 1)
        back.line([(10, 76), (30, 76)], 1)
        back.line([(20, 20), (20, 76)], 1)
    return c.render(seed=15 + flipped)


def marker_assist():
    img = Image.new("RGBA", (64, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((2, 3, 61, 13), outline=AGED_PAPER + (150,), width=1)
    d.line((26, 5, 38, 11), fill=AGED_PAPER + (170,), width=1)   # 粉笔叉
    d.line((26, 11, 38, 5), fill=AGED_PAPER + (170,), width=1)
    return img


# ---------------------------------------------------------------- 观众（台前一排后脑勺）

def audience(kind):
    c = Canvas(80, 96)
    rnd = random.Random(kind * 31 + 5)
    shoulders = 26 + rnd.randint(-3, 4)
    c.ellipse((40 - shoulders, 58, 40 + shoulders, 130))
    head_r = 14 + rnd.randint(-2, 2)
    hy = 46
    c.ellipse((40 - head_r, hy - head_r, 40 + head_r, hy + head_r + 2))
    c.rect((34, hy + head_r - 2, 46, 64))         # 脖子
    if kind == 0:                                 # 圆顶礼帽
        c.ellipse((40 - head_r - 6, hy - 10, 40 + head_r + 6, hy - 4))
        c.ellipse((40 - head_r + 1, hy - head_r - 12, 40 + head_r - 1, hy - 2))
    elif kind == 1:                               # 盘发
        c.ellipse((30, hy - head_r - 10, 50, hy - head_r + 6))
    elif kind == 2:                               # 鸭舌帽
        c.ellipse((40 - head_r - 1, hy - head_r - 3, 40 + head_r + 1, hy + 2))
        c.poly([(40 + head_r - 4, hy - 4), (40 + head_r + 10, hy - 1), (40 + head_r - 2, hy + 1)])
    elif kind == 3:                               # 带羽毛的女帽
        c.ellipse((40 - head_r - 8, hy - 8, 40 + head_r + 8, hy - 1))
        c.poly([(46, hy - 8), (64, hy - 34), (56, hy - 8)])
    elif kind == 4:                               # 秃顶，两只大耳朵
        c.ellipse((40 - head_r - 4, hy - 3, 40 - head_r + 4, hy + 8))
        c.ellipse((40 + head_r - 4, hy - 3, 40 + head_r + 4, hy + 8))
    # kind 5：什么也不戴
    return c.render(rim=False, seed=40 + kind)


def watcher():
    """前排的"他"：同样是后脑勺，略微侧过头。两只眼睛是单独一张图（watcher_eyes），可以跟着追光转。"""
    c = Canvas(80, 96)
    c.ellipse((10, 58, 70, 130))
    c.ellipse((25, 30, 55, 62))
    c.rect((34, 58, 46, 66))
    c.ellipse((18, 26, 62, 34))                   # 宽檐软呢帽
    c.ellipse((27, 12, 53, 30))
    return c.render(rim=False, seed=99)


def watcher_eyes():
    img = Image.new("RGBA", (16, 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((1, 1, 4, 4), fill=AGED_PAPER + (255,))
    d.ellipse((10, 1, 13, 4), fill=AGED_PAPER + (255,))
    return img


# ---------------------------------------------------------------- 特效 / 标题

def eye_flash():
    img = Image.new("RGBA", (64 * S, 40 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # 杏仁形眼眶
    pts = []
    for i in range(41):
        t = i / 40
        x = 4 + t * 56
        pts.append((x * S, (20 - math.sin(t * math.pi) * 15) * S))
    for i in range(41):
        t = 1 - i / 40
        x = 4 + t * 56
        pts.append((x * S, (20 + math.sin(t * math.pi) * 15) * S))
    d.polygon(pts, fill=AGED_PAPER + (255,))
    d.ellipse((24 * S, 12 * S, 40 * S, 28 * S), fill=NEAR_BLACK + (255,))
    return img.resize((64, 40), Image.LANCZOS)


def title_card():
    W, H = 640, 360
    img = Image.new("RGBA", (W, H), NEAR_BLACK + (255,))
    beam = Image.new("L", (W, H), 0)
    bd = ImageDraw.Draw(beam)
    bd.polygon([(290, 0), (350, 0), (430, 300), (210, 300)], fill=70)
    bd.ellipse((200, 284, 440, 316), fill=110)
    beam = beam.filter(ImageFilter.GaussianBlur(1))
    img.paste(Image.new("RGBA", (W, H), SODIUM_YELLOW + (255,)), (0, 0), beam)

    # 台上只剩一顶过高的高礼帽和一根弯魔杖
    c = Canvas(W, H)
    c.poly([(286, 300), (354, 300), (354, 294), (286, 294)])
    c.poly([(296, 295), (344, 295), (348, 196), (300, 196)])
    c.limb([(250, 302), (300, 302), (336, 286)], 3)
    hat = c.render(seed=7)
    img.alpha_composite(hat)
    tip = ImageDraw.Draw(img)
    tip.line((322, 293, 336, 286), fill=AGED_PAPER + (255,), width=3)
    return add_grain(img, 5, 1)


def main():
    os.makedirs(OUT, exist_ok=True)
    print("写入", OUT)
    for name, pose in MAGICIAN.items():
        save(magician(pose), name)
    save(box_open(), "prop_box_open")
    save(box_sealed(), "prop_box_sealed")
    save(cloth_off(), "prop_cloth_off")
    save(cloth_on(), "prop_cloth_on")
    save(mirror(False), "prop_mirror_normal")
    save(mirror(True), "prop_mirror_flipped")
    save(marker_assist(), "prop_marker_assist")
    for k in range(6):
        save(audience(k), f"audience_{k:02d}")
    save(watcher(), "audience_watcher")
    save(watcher_eyes(), "watcher_eyes")
    save(eye_flash(), "eye_flash")
    save(title_card(), "title_card")


if __name__ == "__main__":
    main()
