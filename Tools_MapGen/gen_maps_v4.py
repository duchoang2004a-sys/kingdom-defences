#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
King of Rush - Bộ sinh 10 map v4 (khung 1920x1080 = 19.2 x 10.8 đơn vị @100 PPU).
Ghi level_XX_map.json (schema KRMap) vào Assets/_Project/Sprites/Maps (+ 2 bản sao mà KRAssetFinder có thể chọn).
Chạy:  python3 Tools_MapGen/gen_maps_v4.py [--preview out.png]
Hằng số phải khớp Assets/_Project/Scripts/Core/GameScale.cs (PATH_W, CASTLE_SCALE, VIEW).
"""
import json, math, os, sys, random, struct, collections
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..'))
SPR = os.path.join(ROOT, 'Assets', '_Project', 'Sprites')
MAPS_DIR = os.path.join(SPR, 'Maps')
COPY_DIRS = [MAPS_DIR, os.path.join(MAPS_DIR, 'Maps'), os.path.join(SPR, 'Extra', 'Maps')]
PROPS_DIR = os.path.join(SPR, 'Props')

VIEW_HW, VIEW_HH = 9.6, 5.4           # nửa khung nhìn (ortho 5.4, 16:9)
PATH_W = 1.2                           # GameScale.PathWidth
PATH_TILE_R = 0.64                     # tâm ô nền cách trục path <= R => ô đường
CELL_W, CELL_H = 0.48, 0.24
CASTLE_SCALE = 0.42                    # GameScale.Castle
CASTLE_W, CASTLE_H, CASTLE_GATE = 3.2, 3.3, 0.78   # sprite castle (đơn vị) + cổng cách đáy
PAD_MIN_D, PAD_MAX_D = 1.0, 1.75       # tâm ô xây cách trục path
RANGE_R = 1.9                          # bán kính tính độ phủ (≈ tầm bắn cấp 1)
MIN_SLOT_GAP = 2.3
SAFE = (-9.0, 9.0, -4.45, 4.5)         # vùng cho phép đặt ô xây (tránh HUD trên/dưới)

# ----------------------------------------------------------------------------- hình học
def d2(a, b): return math.hypot(a[0] - b[0], a[1] - b[1])

def catmull(P, step=0.2, alpha=0.5):
    """Spline Catmull-Rom hướng tâm: đường lượn mềm, không vọt biên."""
    Q = [(2 * P[0][0] - P[1][0], 2 * P[0][1] - P[1][1])] + list(P) + [(2 * P[-1][0] - P[-2][0], 2 * P[-1][1] - P[-2][1])]
    out = []
    for i in range(1, len(Q) - 2):
        p0, p1, p2, p3 = Q[i - 1], Q[i], Q[i + 1], Q[i + 2]
        t0 = 0.0
        t1 = t0 + max(1e-6, d2(p0, p1)) ** alpha
        t2 = t1 + max(1e-6, d2(p1, p2)) ** alpha
        t3 = t2 + max(1e-6, d2(p2, p3)) ** alpha
        n = max(2, int(d2(p1, p2) / step))
        for k in range(n):
            t = t1 + (t2 - t1) * k / n
            def mix(a, b, ta, tb): return ((tb - t) / (tb - ta) * a[0] + (t - ta) / (tb - ta) * b[0],
                                           (tb - t) / (tb - ta) * a[1] + (t - ta) / (tb - ta) * b[1])
            A1 = mix(p0, p1, t0, t1); A2 = mix(p1, p2, t1, t2); A3 = mix(p2, p3, t2, t3)
            B1 = mix(A1, A2, t0, t2); B2 = mix(A2, A3, t1, t3)
            out.append(mix(B1, B2, t1, t2))
    out.append(P[-1])
    return out

def resample(P, step):
    out = [P[0]]; acc = 0.0
    for i in range(1, len(P)):
        a, b = P[i - 1], P[i]; seg = d2(a, b)
        if seg < 1e-9: continue
        pos = step - acc
        while pos <= seg:
            t = pos / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)); pos += step
        acc = (acc + seg) % step if seg < step - acc else seg - (pos - step)
    if d2(out[-1], P[-1]) > 0.02: out.append(P[-1])
    else: out[-1] = P[-1]
    return out

def length(P): return sum(d2(P[i], P[i + 1]) for i in range(len(P) - 1))

def png_size(name):
    for root in (PROPS_DIR, os.path.join(SPR, 'Extra', 'Props')):
        p = os.path.join(root, name + '.png')
        if os.path.exists(p):
            with open(p, 'rb') as f: h = f.read(24)
            return struct.unpack('>II', h[16:24])
    return (100, 100)

def castle_rect(c):
    cx, cy = c
    return (cx - CASTLE_SCALE * CASTLE_W / 2, cx + CASTLE_SCALE * CASTLE_W / 2,
            cy - CASTLE_SCALE * CASTLE_GATE, cy + CASTLE_SCALE * (CASTLE_H - CASTLE_GATE))

def dist_rect(x, y, r):
    dx = max(r[0] - x, 0, x - r[1]); dy = max(r[2] - y, 0, y - r[3])
    return math.hypot(dx, dy)

def min_dist(points, S, chunk=800):
    """Khoảng cách nhỏ nhất từ mỗi điểm (N,2) tới tập mẫu S (M,2)."""
    out = np.empty(len(points))
    for i in range(0, len(points), chunk):
        p = points[i:i + chunk]
        out[i:i + chunk] = np.sqrt(((p[:, None, :] - S[None, :, :]) ** 2).sum(-1)).min(1)
    return out

# ----------------------------------------------------------------------------- 10 màn (thiết kế tay)
T = lambda *a: list(a)
def level_defs():
    """10 thiết kế tay (toạ độ world, x:[-8.7,8.7], y:[-3.5,3.5]). Mỗi màn một hình học khác hẳn:
    1 rắn ngang | 2 hai cổng hợp lưu - thân rắn 4 hàng | 3 rắn dọc 6 cột | 4 zigzag chéo | 5 rẽ đôi quanh đảo
    6 sông + 3 cầu (1 cổng) | 7 xoắn ốc vào thành | 8 gọng kìm + thân 2 hàng | 9 ba cổng + vòng dung nham | 10 rắn 4 hàng 2 cổng.
    minlen = độ dài tối thiểu từng tuyến (mặc định 40; tuyến phụ của màn nhiều cổng được nới nhẹ)."""
    L = {}
    # 1 Đường Mòn - rắn ngang 3 hàng, 1 đường
    L[1] = dict(theme='grass', amb='leaves', slots=9, decor=260,
        routes=[T((-8.7,3.2),(-6.0,3.2),(-2.6,3.6),(1.4,3.0),(5.0,3.5),(7.3,2.6),(6.0,0.9),(2.6,0.5),(-1.0,1.0),(-4.4,0.4),(-7.8,0.5),(-7.9,-1.7),(-5.2,-2.9),(-1.8,-3.3),(2.0,-2.8),(5.2,-3.2),(7.9,-3.0),(7.9,-0.9))],
        castle=(7.9,-0.9), tip='Đường dài 3 hàng uốn lượn: đặt tháp ở khoảng giữa các hàng để bắn được 2 đoạn đường cùng lúc')
    # 2 Ngã Rẽ - 2 cổng hợp lưu giữa bản đồ rồi thân rắn 4 hàng nửa phải, lâu đài ở giữa-trên
    A = T((-8.7,3.3),(-6.2,3.3),(-3.3,2.7),(-1.3,1.0),(-0.6,0.0)); B = T((-8.7,-3.1),(-6.2,-3.1),(-3.5,-2.5),(-1.5,-1.0),(-0.6,0.0))
    TR = T((0.2,-1.7),(1.4,-3.1),(3.2,-3.4),(6.8,-3.4),(8.2,-2.25),(6.8,-1.1),(3.0,-1.1),(1.7,0.05),(3.0,1.2),(6.8,1.2),(8.2,2.35),(6.8,3.5),(3.0,3.5))
    L[2] = dict(theme='grass', amb='petals', slots=10, decor=290, routes=[A + TR, B + TR], castle=(3.0,3.5),
        tip='Hai cổng quái hợp lưu ở giữa bản đồ: tháp đặt gần điểm hợp lưu sẽ bắn được cả hai nhánh')
    # 3 Cánh Đồng - rắn dọc 6 cột
    L[3] = dict(theme='farm', amb='leaves', slots=11, decor=230,
        routes=[T((8.7,3.4),(7.0,3.4),(7.6,0.4),(7.0,-3.3),(4.4,-3.3),(4.2,0.2),(4.4,3.3),(1.5,3.4),(1.7,0.2),(1.5,-3.3),(-1.4,-3.3),(-1.6,0.2),(-1.4,3.3),(-4.3,3.4),(-4.1,0.2),(-4.3,-3.3),(-7.2,-3.3),(-7.2,1.0))],
        castle=(-7.2,1.0), tip='Quái đi hết 6 hàng ruộng dọc: tháp ở giữa hai hàng bắn được cả hai phía')
    # 4 Hẻm Đá - zigzag chéo
    L[4] = dict(theme='sand', amb='dust', slots=11, decor=220,
        routes=[T((-8.7,-3.3),(-7.9,-3.3),(-4.9,3.4),(-1.9,-3.2),(1.1,3.4),(4.1,-3.2),(7.0,3.3),(8.0,3.3),(8.0,0.8))],
        castle=(8.0,0.8), tip='Hẻm đá zigzag: góc cua là điểm vàng đặt tháp pháo')
    # 5 Giao Lộ - thân rắn bên trái -> rẽ đôi quanh đảo -> hợp lưu -> thân dài chạy xuống đáy
    S5 = T((-8.7,-3.4),(-7.0,-3.4),(-4.6,-3.4),(-3.5,-2.25),(-4.6,-1.1),(-7.4,-1.1),(-8.5,0.05),(-7.4,1.2),(-4.6,1.2),(-3.4,1.6))
    UP = T((-2.2,3.0),(0.5,3.6),(4.0,3.5),(6.8,3.1),(7.9,1.8)); LO = T((-2.2,0.3),(1.0,0.0),(4.0,0.15),(6.4,0.6),(7.9,1.8))
    TR5 = T((8.3,-0.9),(7.4,-3.1),(5.0,-3.4),(1.5,-3.4),(-1.2,-3.4))
    L[5] = dict(theme='forest', amb='leaves', slots=12, decor=520, routes=[S5 + UP + TR5, S5 + LO + TR5], castle=(-1.2,-3.4),
        tip='Đường rẽ đôi quanh một đảo giữa: tháp trên đảo bắn được cả hai nhánh')
    # 6 Kẻ Dẫn Đường - sông + 3 cầu, 1 cổng
    A6 = T((-8.7,3.2),(-6.0,3.2),(-3.2,3.2),(0.4,3.4),(4.2,3.0),(7.0,1.8),(5.6,0.2),(2.4,-0.2),(-1.0,0.2),(-4.6,-0.4),(-6.8,-1.8),(-5.0,-3.2))
    TR6 = T((-2.0,-3.3),(2.0,-3.0),(5.0,-3.4),(7.9,-3.0),(7.9,-0.8))
    L[6] = dict(theme='grass', amb='rain', slots=12, decor=280, routes=[A6 + TR6], castle=(7.9,-0.8),
        river=dict(pts=T((-0.4,6.2),(-1.6,3.0),(-0.2,0.0),(-1.6,-3.0),(-0.6,-6.2)), kind='water'),
        tip='Mưa và sông: quái băng qua 3 cây cầu, hãy chặn trước mỗi cầu')
    # 7 Thành Lũy - xoắn ốc vào lâu đài giữa
    L[7] = dict(theme='fort', amb='dark', slots=13, decor=250,
        routes=[T((-8.7,-3.4),(-5.0,-3.5),(0.0,-3.4),(5.0,-3.5),(8.0,-2.8),(8.2,0.4),(7.8,3.2),(4.0,3.5),(0.0,3.3),(-4.0,3.5),(-7.6,3.0),(-8.0,0.6),(-7.4,-1.0),(-4.0,-1.1),(0.0,-0.9),(3.0,-1.0),(3.0,0.6))],
        castle=(3.0,0.6), tip='Xoắn ốc vào thành: đoạn trong gần lâu đài rất nguy hiểm, giữ lính chặn ở đó')
    # 8 Băng Giá - gọng kìm 2 cổng bên phải, hợp lưu bên trái, thân rắn 2 hàng quay về lâu đài
    A8 = T((8.7,3.5),(5.0,3.6),(1.0,3.4),(-3.0,3.5),(-6.2,3.3),(-7.7,2.2),(-7.2,1.2))
    B8 = T((8.7,-3.5),(5.0,-3.6),(1.0,-3.4),(-3.0,-3.5),(-6.4,-3.4),(-8.3,-2.3),(-8.3,-0.2),(-7.7,1.0),(-7.2,1.2))
    TR8 = T((-5.6,1.2),(-2.0,1.15),(2.0,1.15),(6.0,1.25),(7.8,0.1),(6.0,-1.15),(2.0,-1.15),(-1.5,-1.15))
    L[8] = dict(theme='snow', amb='snow', slots=13, decor=300, routes=[A8 + TR8, B8 + TR8], castle=(-1.5,-1.15),
        tip='Gọng kìm: hai nhánh trên dưới gặp nhau ở bên trái rồi quay về lâu đài theo hai hàng giữa')
    # 9 Núi Lửa - 3 cổng bên trái hội tụ, vòng dài quanh núi lửa, sông dung nham
    A9 = T((-8.7,3.3),(-5.0,3.5),(-2.4,3.2),(-1.5,1.7),(-3.0,0.2)); B9 = T((-8.7,0.1),(-7.3,1.2),(-5.9,-0.9),(-4.5,1.0),(-3.0,0.2))
    C9 = T((-8.7,-3.3),(-5.4,-3.5),(-3.4,-3.0),(-3.6,-1.4),(-3.0,0.2))
    TR9 = T((-1.0,0.3),(1.8,0.7),(3.4,1.7),(3.6,3.3),(6.2,3.45),(7.7,2.2),(6.4,0.3),(3.9,-0.8),(1.5,-1.5),(0.4,-2.7),(1.9,-3.4),(5.8,-3.4),(7.9,-3.2))
    L[9] = dict(theme='volcano', amb='embers', slots=14, decor=330, routes=[A9 + TR9, B9 + TR9, C9 + TR9], castle=(7.9,-3.2), minlen=[38,34,38],
        river=dict(pts=T((0.2,6.2),(-0.6,3.0),(0.4,0.0),(-0.4,-3.0),(0.6,-6.2)), kind='lava'),
        tip='Ba cổng dung nham hội tụ rồi đi một vòng dài quanh núi lửa')
    # 10 Ngai Rồng - rắn 4 hàng, 2 cổng bên phải
    R10 = T((8.7,3.5),(5.0,3.7),(1.0,3.4),(-3.0,3.7),(-6.8,3.3),(-8.2,2.35),(-6.8,1.2),(-3.0,1.0),(1.0,1.3),(5.0,1.0),(7.5,0.0),(8.2,-1.2))
    R10b = T((6.2,-1.2),(1.0,-1.3),(-3.0,-1.1),(-6.8,-1.3),(-8.2,-2.4),(-6.8,-3.5),(-3.0,-3.6),(1.0,-3.4),(5.0,-3.6),(7.9,-3.5))
    B10 = T((8.7,-1.0),(7.4,-1.1))
    L[10] = dict(theme='void', amb='motes', slots=15, decor=330, routes=[R10 + R10b, B10 + T((6.2,-1.2)) + R10b[1:]], castle=(7.9,-3.5), minlen=[40,30],
        tip='Hai cổng, đường dài nhất game: cổng phụ ngắn hơn nhưng quái mạnh hơn dồn về lâu đài')
    return L

# ----------------------------------------------------------------------------- dữ liệu tile theo theme (đọc từ JSON cũ)
def load_old(n):
    for d in COPY_DIRS:
        p = os.path.join(d, 'level_%02d_map.json' % n)
        if os.path.exists(p):
            return json.load(open(p, encoding='utf-8-sig'))
    raise SystemExit('Thiếu level_%02d_map.json để lấy danh sách tile' % n)

def tile_roles(old):
    ids = old['tileIds']; gc = collections.Counter(old['gt']); dc = collections.Counter(old['dt'])
    role = dict(ground=[], path=None, bridge=None, water=None, decor=[], spawn=None)
    for k, c in gc.items():
        i = ids[k]
        if i.startswith('path'): role['path'] = i
        elif i.startswith('bridge'): role['bridge'] = i
        elif i.startswith('water') or i.startswith('lava'): role['water'] = i
        else: role['ground'].append((i, c))
    for k, c in dc.items():
        i = ids[k]
        if i.startswith('spawn'): role['spawn'] = i
        else: role['decor'].append((i, c))
    role['ground'].sort(); role['decor'].sort()
    return role

# ----------------------------------------------------------------------------- sinh một map
def build_level(n, cfg, old, rng):
    role = tile_roles(old)
    if role['water'] is None:   # map không có sông: lấy từ map 6 (nước) / 9 (dung nham) nếu cần
        pass
    # --- tuyến
    ctrl = cfg['routes']; castle = cfg['castle']
    dense = [resample(catmull(c, 0.2), 0.15) for c in ctrl]
    way = []
    for d in dense:
        w = resample(d, 0.7); w[-1] = d[-1]; way.append(w)
    S = np.array([p for d in dense for p in d])
    # --- ô nền
    cells = []
    for gx in range(-64, 65):
        for gy in range(-64, 65):
            x = (gx - gy) * CELL_W * 0.5; y = (gx + gy) * CELL_H * 0.5
            if abs(x) <= VIEW_HW + 1.5 and abs(y) <= VIEW_HH + 0.8: cells.append((gx, gy, x, y))
    C = np.array([(c[2], c[3]) for c in cells])
    dpath = min_dist(C, S)
    river = None; driver = np.full(len(cells), 9.0)
    if cfg.get('river'):
        rd = np.array(resample(catmull(cfg['river']['pts'], 0.2), 0.15)); driver = min_dist(C, rd); river = cfg['river']['kind']
    # --- slot
    U = []
    for d in dense:
        for p in resample(d, 0.5):
            if not any(d2(p, q) < 0.3 for q in U): U.append(p)
    U = np.array(U)
    wgt = np.array([sum(1 for d in dense if min(d2(u, q) for q in d[::2]) < 0.45) for u in U], dtype=float)
    xs = np.arange(SAFE[0], SAFE[1] + 0.01, 0.25); ys = np.arange(SAFE[2], SAFE[3] + 0.01, 0.25)
    cand = np.array([(x, y) for x in xs for y in ys])
    cd = min_dist(cand, S)
    cr = castle_rect(castle); spawn_pts = list({(r[0][0], r[0][1]) for r in ctrl})
    ok = (cd >= PAD_MIN_D) & (cd <= PAD_MAX_D)
    if river: ok &= min_dist(cand, np.array(resample(catmull(cfg['river']['pts'], 0.2), 0.15))) >= 1.05
    ok &= np.array([dist_rect(x, y, cr) >= 0.75 for x, y in cand])
    ok &= np.array([all(d2((x, y), s) >= 1.5 for s in spawn_pts) for x, y in cand])
    cand = cand[ok]; cdok = cd[ok]
    cov = (np.sqrt(((cand[:, None, :] - U[None, :, :]) ** 2).sum(-1)) <= RANGE_R)
    # hướng đi (tiếp tuyến) để biết ô nằm trái/phải đường
    nearest = np.array([np.argmin(((S - c) ** 2).sum(1)) for c in cand])
    tang = np.zeros_like(S); off = 0
    for d in dense:
        a = np.array(d)
        t = np.gradient(a, axis=0); t /= np.maximum(1e-9, np.linalg.norm(t, axis=1))[:, None]
        tang[off:off + len(d)] = t; off += len(d)
    side = np.sign(tang[nearest, 0] * (cand[:, 1] - S[nearest, 1]) - tang[nearest, 1] * (cand[:, 0] - S[nearest, 0]))
    covered = np.zeros(len(U)); chosen = []; left = right = 0
    for _ in range(cfg['slots']):
        gain = (cov * (wgt / (1.0 + covered) ** 1.5)[None, :]).sum(1) * (1.15 - 0.15 * (cdok - PAD_MIN_D))
        for ci in chosen: gain[np.hypot(cand[:, 0] - cand[ci, 0], cand[:, 1] - cand[ci, 1]) < MIN_SLOT_GAP] = -1
        if abs(left - right) >= 2: gain[side == (1 if left > right else -1)] *= 0.55
        b = int(np.argmax(gain))
        if gain[b] <= 0: break
        chosen.append(b); covered += cov[b]
        if side[b] > 0: left += 1
        else: right += 1
    slots = [dict(x=round(float(cand[b, 0]), 2), y=round(float(cand[b, 1]), 2), score=round(float(cov[b].sum()), 1), side=int(side[b])) for b in chosen]
    slots.sort(key=lambda s: (s['x'], s['y']))
    for i, s in enumerate(slots): s['id'] = i + 1
    SL = np.array([(s['x'], s['y']) for s in slots]) if slots else np.zeros((0, 2))
    # --- tile nền
    tileIds = []
    def tid(name):
        if name not in tileIds: tileIds.append(name)
        return tileIds.index(name)
    grounds = role['ground']; gsum = float(sum(c for _, c in grounds)); gx = []; gy = []; gt = []
    for k, (cgx, cgy, x, y) in enumerate(cells):
        if dpath[k] <= PATH_TILE_R:
            name = role['bridge'] if (river and driver[k] <= 0.75 and role['bridge']) else role['path']
        elif river and driver[k] <= 0.75 and role['water']:
            name = role['water']
        else:
            if len(grounds) == 1: name = grounds[0][0]
            else:
                v = (math.sin(0.9 * x + 1.3) * math.sin(0.8 * y - 0.4) + math.sin(0.37 * x + 0.53 * y)) / 2.0 * 0.5 + 0.5
                acc = 0.0; name = grounds[-1][0]
                for gname, c in grounds:
                    acc += c / gsum
                    if v <= acc: name = gname; break
        gx.append(cgx); gy.append(cgy); gt.append(tid(name))
    # --- trang trí
    allowed = np.array([(dpath[k] >= 0.98 and driver[k] >= 1.0 and dist_rect(c[2], c[3], cr) >= 0.5 and
                         (len(SL) == 0 or np.hypot(SL[:, 0] - c[2], SL[:, 1] - c[3]).min() >= 0.85) and
                         all(d2((c[2], c[3]), s) >= 1.1 for s in spawn_pts)) for k, c in enumerate(cells)])
    idx = [k for k in range(len(cells)) if allowed[k]]
    rng.shuffle(idx)
    free_area = len(idx) * CELL_W * CELL_H / 2.0
    sp = max(0.55, math.sqrt(free_area / (cfg['decor'] * 1.25)))
    dec = role['decor']; dsum = float(sum(c for _, c in dec)); acc_pts = []; grid = {}; dx = []; dy = []; dt = []
    for k in idx:
        if len(dx) >= cfg['decor']: break
        x, y = cells[k][2], cells[k][3]; gk = (int(x // sp), int(y // sp)); bad = False
        for ax in (-1, 0, 1):
            for ay in (-1, 0, 1):
                for q in grid.get((gk[0] + ax, gk[1] + ay), ()):
                    if d2(q, (x, y)) < sp: bad = True; break
                if bad: break
            if bad: break
        if bad: continue
        grid.setdefault(gk, []).append((x, y))
        r = rng.random() * dsum; acc = 0.0; name = dec[-1][0]
        for dname, c in dec:
            acc += c
            if r <= acc: name = dname; break
        dx.append(cells[k][0]); dy.append(cells[k][1]); dt.append(tid(name))
    # --- cổng spawn (ô trang trí) + prop
    spawns = []
    for i, p in enumerate(spawn_pts):
        spawns.append(dict(label='Spawn ' + chr(65 + i), x=round(p[0], 2), y=round(p[1], 2)))
        if role['spawn']:
            kk = int(np.argmin(((C - np.array(p)) ** 2).sum(1)))
            dx.append(cells[kk][0]); dy.append(cells[kk][1]); dt.append(tid(role['spawn']))
    props = []
    cname = 'castle_bef1e7' if n == 10 else 'castle_bef441'
    props.append(dict(sprite=cname, x=round(castle[0] - CASTLE_SCALE * CASTLE_W / 2, 3), y=round(castle[1] + CASTLE_SCALE * (CASTLE_H - CASTLE_GATE), 3), order=0))
    if n == 9:
        pw, ph = png_size('portal_c4b35f')
        for s in spawns:
            props.append(dict(sprite='portal_c4b35f', x=round(s['x'] - pw / 200.0, 3), y=round(s['y'] + ph / 200.0, 3), order=0))
        sw, sh = png_size('skull_f913a0')
        for k in idx[:400:100]:
            props.append(dict(sprite='skull_f913a0', x=round(cells[k][2] - sw / 200.0, 3), y=round(cells[k][3] + sh / 200.0, 3), order=0))
    if n == 3:
        fw, fh = png_size('prop_fd87f3')
        for k in idx[40:400:130][:3]:
            props.append(dict(sprite='prop_fd87f3', x=round(cells[k][2] - fw / 200.0, 3), y=round(cells[k][3] + fh / 200.0, 3), order=0))
    out = dict(level=n, name=old['name'], theme=cfg['theme'], imageW=1920, imageH=1080, cellW=CELL_W, cellH=CELL_H, gridOffsetX=0.0, gridOffsetY=0.0,
               spawns=spawns, castle=dict(x=castle[0], y=castle[1]),
               routes=[dict(spawn=spawn_pts.index((r[0][0], r[0][1])), label='Route ' + chr(65 + i), points=[dict(x=round(p[0], 3), y=round(p[1], 3)) for p in way[i]]) for i, r in enumerate(ctrl)],
               slots=[dict(id=s['id'], x=s['x'], y=s['y'], score=s['score']) for s in slots],
               tileIds=tileIds, gx=gx, gy=gy, gt=gt, dx=dx, dy=dy, dt=dt, props=props, hasOverlay=False,
               waves=old['waves'], startGold=old['startGold'], lives=old['lives'], hpMultiplier=old['hpMultiplier'], enemies=old['enemies'],
               boss=old['boss'], tip=cfg['tip'], bossTypes=old['bossTypes'], bossWaves=old['bossWaves'], bossHps=old['bossHps'], ambience=cfg['amb'])
    meta = dict(dense=dense, way=way, slots=slots, cells=cells, gt=gt, tileIds=tileIds, dpath=dpath, driver=driver, castle=castle, cr=cr, ctrl=ctrl,
                dx=dx, dy=dy, role=role, river=cfg.get('river'), minlen=cfg.get('minlen'))
    return out, meta

# ----------------------------------------------------------------------------- kiểm chứng
def validate(n, out, meta):
    rep = []; bad = []
    L = [length(d) for d in meta['dense']]
    rep.append('len=' + '/'.join('%.0f' % l for l in L))
    for ri, d in enumerate(meta['dense']):
        a = np.array(d); acc = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(a, axis=0).T))])
        step = 4; sub = a[::step]; sa = acc[::step]
        D = np.sqrt(((sub[:, None] - sub[None]) ** 2).sum(-1)); A = np.abs(sa[:, None] - sa[None])
        m = D[A > 5.0].min() if (A > 5.0).any() else 99
        rep.append('selfmin%d=%.2f' % (ri, m))
        if m < 1.9: bad.append('route %d tự áp sát %.2f' % (ri, m))
        need = (meta.get('minlen') or [40] * len(meta['dense']))[ri]
        if length(d) < need: bad.append('route %d quá ngắn %.1f (cần >= %d)' % (ri, length(d), need))
        # cách lâu đài (trừ 3 đơn vị cuối)
        cut = a[acc < acc[-1] - 3.0]
        if len(cut):
            cl = min(dist_rect(p[0], p[1], meta['cr']) for p in cut[::3])
            if cl < 0.85: bad.append('route %d sát lâu đài %.2f' % (ri, cl))
        if np.abs(a[:, 0]).max() > 9.0 or a[:, 1].max() > 4.0 or a[:, 1].min() < -3.95: bad.append('route %d ra ngoài vùng an toàn' % ri)
    sl = meta['slots']; SL = np.array([(s['x'], s['y']) for s in sl])
    left = sum(1 for s in sl if s['side'] > 0); right = len(sl) - left
    rep.append('slots=%d (trái %d / phải %d)' % (len(sl), left, right))
    if len(sl) < 6: bad.append('quá ít ô xây')
    if min(left, right) < 3: bad.append('ô xây lệch một phía')
    for i in range(len(sl)):
        for j in range(i + 1, len(sl)):
            if d2((sl[i]['x'], sl[i]['y']), (sl[j]['x'], sl[j]['y'])) < MIN_SLOT_GAP - 0.01: bad.append('2 ô xây quá gần')
    S = np.array([p for d in meta['dense'] for p in d]); dd = min_dist(SL, S)
    rep.append('padDist=%.2f..%.2f' % (dd.min(), dd.max()))
    # chỉ số mảng
    n_g = len(out['gx']); assert len(out['gy']) == n_g == len(out['gt']); assert len(out['dx']) == len(out['dy']) == len(out['dt'])
    assert max(out['gt']) < len(out['tileIds']) and max(out['dt']) < len(out['tileIds'])
    pathn = sum(1 for k in out['gt'] if out['tileIds'][k].startswith('path'))
    rep.append('cells=%d path=%d decor=%d' % (n_g, pathn, len(out['dx'])))
    return rep, bad

def path_mask(meta):
    S = np.array([p for d in meta['dense'] for p in d])
    g = set((int(math.floor(x / 0.6)), int(math.floor(y / 0.6))) for x, y in S)
    return g

def main():
    prev = None
    if '--preview' in sys.argv: prev = sys.argv[sys.argv.index('--preview') + 1]
    only = None
    if '--only' in sys.argv: only = [int(x) for x in sys.argv[sys.argv.index('--only') + 1].split(',')]
    defs = level_defs(); metas = {}; fail = False
    for n in range(1, 11):
        if only and n not in only: continue
        old = load_old(n); rng = random.Random(4000 + n)
        out, meta = build_level(n, defs[n], old, rng)
        rep, bad = validate(n, out, meta)
        print('L%02d %-14s %s %s' % (n, out['name'], ' | '.join(rep), ('  !! ' + '; '.join(bad)) if bad else '  OK'))
        if bad: fail = True
        metas[n] = (out, meta)
        if '--write' in sys.argv:
            for d in COPY_DIRS:
                os.makedirs(d, exist_ok=True)
                with open(os.path.join(d, 'level_%02d_map.json' % n), 'w', encoding='utf-8') as f:
                    json.dump(out, f, ensure_ascii=False, separators=(',', ':'))
    # độ khác biệt giữa các map (IoU mặt nạ đường đi)
    ks = sorted(metas); M = {k: path_mask(metas[k][1]) for k in ks}; worst = (0, None)
    for i in ks:
        for j in ks:
            if i < j:
                iou = len(M[i] & M[j]) / float(len(M[i] | M[j]))
                if iou > worst[0]: worst = (iou, (i, j))
    print('IoU path lớn nhất giữa 2 map: %.2f %s' % worst)
    if prev: render_preview(metas, prev)
    return 1 if fail else 0

def render_preview(metas, out_path):
    from PIL import Image, ImageDraw
    cw, ch, s = 480, 270, 25.0
    ks = sorted(metas); cols = 5; rows = (len(ks) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * cw, rows * ch), (20, 20, 28))
    col = {'grass': (70, 120, 60), 'farm': (110, 130, 60), 'sand': (200, 170, 100), 'forest': (40, 90, 50), 'fort': (90, 90, 100), 'snow': (200, 220, 235), 'volcano': (60, 45, 45), 'void': (60, 40, 90)}
    for idx, k in enumerate(ks):
        out, meta = metas[k]; im = Image.new('RGB', (cw, ch), col.get(out['theme'], (60, 90, 60))); dr = ImageDraw.Draw(im)
        X = lambda x: cw / 2 + x * s; Y = lambda y: ch / 2 - y * s
        for (cg, cy2, x, y), t in zip(meta['cells'], meta['gt']):
            n_ = out['tileIds'][t]
            if n_.startswith('path'): c = (150, 105, 70)
            elif n_.startswith('bridge'): c = (110, 80, 50)
            elif n_.startswith('water'): c = (50, 110, 190)
            elif n_.startswith('lava'): c = (230, 90, 30)
            else: continue
            dr.ellipse([X(x) - 5, Y(y) - 3, X(x) + 5, Y(y) + 3], fill=c)
        for gxx, gyy in zip(meta['dx'], meta['dy']):
            x = (gxx - gyy) * CELL_W * 0.5; y = (gxx + gyy) * CELL_H * 0.5
            dr.ellipse([X(x) - 2, Y(y) - 2, X(x) + 2, Y(y) + 2], fill=(25, 60, 30))
        for r in meta['dense']:
            dr.line([(X(p[0]), Y(p[1])) for p in r], fill=(255, 235, 120), width=1)
        for sl in meta['slots']:
            dr.ellipse([X(sl['x']) - 5, Y(sl['y']) - 5, X(sl['x']) + 5, Y(sl['y']) + 5], outline=(255, 255, 255) if sl['side'] > 0 else (120, 220, 255), width=2)
        cr = meta['cr']; dr.rectangle([X(cr[0]), Y(cr[3]), X(cr[1]), Y(cr[2])], outline=(255, 60, 60), width=2)
        for sp in out['spawns']: dr.ellipse([X(sp['x']) - 6, Y(sp['y']) - 6, X(sp['x']) + 6, Y(sp['y']) + 6], fill=(220, 40, 200))
        dr.rectangle([0, 0, cw - 1, ch - 1], outline=(0, 0, 0)); dr.text((6, 4), 'L%d %s  %s' % (k, out['name'], out['ambience']), fill=(255, 255, 255))
        sheet.paste(im, ((idx % cols) * cw, (idx // cols) * ch))
    sheet.save(out_path); print('preview ->', out_path)

if __name__ == '__main__':
    sys.exit(main())
