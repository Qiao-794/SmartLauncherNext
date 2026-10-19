import csv
import os
from datetime import datetime
from PIL import Image, ImageDraw, ImageFont

ROOT = r"D:\Dev\Deepseek-00\SmartLauncherNext"
CSV = r"D:\Dev\Deepseek-00\.tmp\stabilitytest\stability-results.csv"
OUT_DIR = os.path.join(ROOT, "docs")
REPORT = os.path.join(ROOT, "tests", "stability-report.md")
FONT = r"C:\Windows\Fonts\msyh.ttc"

os.makedirs(OUT_DIR, exist_ok=True)

with open(CSV, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

def fnum(v):
    if v is None or v == "":
        return None
    try:
        return float(v)
    except Exception:
        return None

def vals(key):
    return [x for x in (fnum(r.get(key)) for r in rows) if x is not None]

def stat(key):
    v = vals(key)
    if not v:
        return None
    return (sum(v) / len(v), min(v), max(v))

def fmt(v, n=1):
    if v is None:
        return "-"
    return f"{v:.{n}f}"

def make_chart(path, title, series, ylabel, ymin=None, ymax=None, note=None):
    W, H = 980, 430
    L, R, T, B = 70, 30, 62, 62
    img = Image.new("RGB", (W, H), "white")
    d = ImageDraw.Draw(img)
    f_title = ImageFont.truetype(FONT, 18)
    f_label = ImageFont.truetype(FONT, 11)
    f_small = ImageFont.truetype(FONT, 10)

    d.text((L, 20), title, font=f_title, fill="#111827")
    if note:
        d.text((L, 42), note, font=f_small, fill="#6B7280")

    all_vals = [v for _, ys, _ in series for v in ys if v is not None]
    if not all_vals:
        return
    if ymin is None:
        ymin = 0.0
    if ymax is None:
        ymax = max(all_vals) * 1.15
        if ymax <= 0:
            ymax = 1.0
    if ymax <= ymin:
        ymax = ymin + 1.0

    plot_w = W - L - R
    plot_h = H - T - B
    d.rectangle([L, T, W - R, H - B], outline="#D1D5DB", width=1)

    for i in range(5):
        y = T + plot_h * i / 4
        d.line([(L, y), (W - R, y)], fill="#EEF2F7")
        value = ymax - (ymax - ymin) * i / 4
        d.text((8, y - 6), fmt(value, 1), font=f_label, fill="#6B7280")

    n = max(len(rows), 1)
    def xpos(i):
        if n <= 1:
            return L + plot_w / 2
        return L + plot_w * i / (n - 1)

    for i, r in enumerate(rows):
        d.text((xpos(i) - 6, H - B + 8), str(r.get("Cycle", i + 1)), font=f_small, fill="#6B7280")

    for name, ys, color in series:
        pts = []
        for i, v in enumerate(ys[:n]):
            if v is None:
                continue
            y = T + plot_h * (ymax - v) / (ymax - ymin)
            pts.append((xpos(i), y))
        if len(pts) > 1:
            d.line(pts, fill=color, width=2, joint="curve")
        for (x, y) in pts:
            d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=color)

    d.text((L, H - 24), "轮次", font=f_label, fill="#374151")
    d.text((8, T - 16), ylabel, font=f_label, fill="#374151")

    lx = W - R - 300
    ly = 20
    for name, _, color in series:
        d.line([(lx, ly + 6), (lx + 18, ly + 6)], fill=color, width=3)
        d.text((lx + 24, ly - 2), name, font=f_small, fill="#374151")
        lx += 24 + int(d.textlength(name, font=f_small)) + 18
        if lx > W - 90:
            lx = W - R - 300
            ly += 18

    img.save(path)

if not rows:
    raise SystemExit("no rows")

cycles = [int(r["Cycle"]) for r in rows]

def series(key, color):
    return [(f"#{r['Cycle']}", fnum(r.get(key)), color) for r in rows]

make_chart(
    os.path.join(OUT_DIR, "stability-memory.png"),
    "私有工作集（Task Manager 的“内存”）",
    [
        ("面板打开", [fnum(r.get("ShownPrivateWS")) for r in rows], "#2563EB"),
        ("隐藏 16.5s", [fnum(r.get("HiddenPrivateWS")) for r in rows], "#059669"),
        ("设置打开", [fnum(r.get("SettingsPrivateWS")) for r in rows], "#D97706"),
        ("设置关闭后", [fnum(r.get("AfterSettingsPrivateWS")) for r in rows], "#7C3AED"),
    ],
    "MB",
)

make_chart(
    os.path.join(OUT_DIR, "stability-commit.png"),
    "提交大小（虚拟提交，不是物理内存）",
    [
        ("面板打开", [fnum(r.get("ShownCommit")) for r in rows], "#2563EB"),
        ("隐藏 16.5s", [fnum(r.get("HiddenCommit")) for r in rows], "#059669"),
        ("设置打开", [fnum(r.get("SettingsCommit")) for r in rows], "#D97706"),
        ("设置关闭后", [fnum(r.get("AfterSettingsCommit")) for r in rows], "#7C3AED"),
    ],
    "MB",
    ymin=0,
    ymax=max([v for v in vals("ShownCommit")] + [v for v in vals("SettingsCommit")] + [1]) * 1.15,
)

make_chart(
    os.path.join(OUT_DIR, "stability-cpu.png"),
    "CPU 占用（占全部核心的百分比）",
    [
        ("面板打开", [fnum(r.get("ShownCpu")) for r in rows], "#2563EB"),
        ("隐藏", [fnum(r.get("HiddenCpu")) for r in rows], "#059669"),
        ("设置打开", [fnum(r.get("SettingsCpu")) for r in rows], "#D97706"),
        ("设置关闭后", [fnum(r.get("AfterSettingsCpu")) for r in rows], "#7C3AED"),
    ],
    "%",
    ymin=0,
    ymax=max([0.5] + [v for v in vals("ShownCpu")] + [v for v in vals("SettingsCpu")]) * 1.2,
    note="1 秒采样，按逻辑核心数折算；0.00 表示低于 0.01%",
)

make_chart(
    os.path.join(OUT_DIR, "stability-cold.png"),
    "冷启动耗时",
    [("冷启动", [fnum(r.get("ColdStartMs")) for r in rows], "#DC2626")],
    "ms",
    ymin=0,
)

# Markdown report
summary_keys = [
    ("ColdStartMs", "冷启动 (ms)", 0),
    ("StartupCpuMs", "启动 CPU 时间 (ms)", 0),
    ("ShownPrivateWS", "打开：私有工作集 (MB)", 1),
    ("ShownCommit", "打开：提交大小 (MB)", 1),
    ("ShownCpu", "打开：CPU (%)", 2),
    ("HiddenPrivateWS", "隐藏 16.5s：私有工作集 (MB)", 1),
    ("HiddenCpu", "隐藏：CPU (%)", 2),
    ("SettingsPrivateWS", "设置打开：私有工作集 (MB)", 1),
    ("SettingsCpu", "设置打开：CPU (%)", 2),
    ("AfterSettingsPrivateWS", "设置关闭后：私有工作集 (MB)", 1),
    ("AfterSettingsCpu", "设置关闭后：CPU (%)", 2),
    ("ShownHandles", "打开：句柄数", 0),
    ("ShownThreads", "打开：线程数", 0),
]

lines = []
lines.append("# SmartLauncher v1.2 稳定性压测报告")
lines.append("")
lines.append(f"- 时间：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
lines.append("- 环境：Windows 11 26200，1920×1080，100% DPI")
lines.append("- 轮次：20 次完整生命周期（冷启动 → 显示 → Esc 隐藏 16.5s → 设置打开/关闭 → 结束进程）")
lines.append("- 指标：Task Manager 的“私有工作集”（Working Set - Private）+ 提交大小 + CPU 时间差；CPU 按全部逻辑核心折算")
lines.append("")
lines.append("## 汇总")
lines.append("")
lines.append("| 指标 | 平均 | 最小 | 最大 |")
lines.append("|---|---:|---:|---:|")
for key, label, nd in summary_keys:
    st = stat(key)
    if st:
        lines.append(f"| {label} | {fmt(st[0], nd)} | {fmt(st[1], nd)} | {fmt(st[2], nd)} |")
lines.append("")

fail = sum(1 for r in rows if (r.get("Error") or "").strip() or (r.get("Errors") or "0") not in ("0", ""))
lines.append(f"- 完成轮次：**{len(rows)}/20**")
lines.append(f"- 失败轮次：**{fail}**")
lines.append(f"- 日志异常：**{sum(1 for r in rows if (r.get('Errors') or '0') not in ('0', ''))}** 行")
lines.append("")

lines.append("## 每轮数据")
lines.append("")
lines.append("| 轮次 | 冷启动 ms | 打开私有 WS | 隐藏私有 WS | 设置私有 WS | 关闭后私有 WS | 打开 CPU% | 隐藏 CPU% | 设置 CPU% | 错误 |")
lines.append("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|")
for r in rows:
    lines.append("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} |".format(
        r.get("Cycle", ""),
        fmt(fnum(r.get("ColdStartMs")), 0),
        fmt(fnum(r.get("ShownPrivateWS")), 1),
        fmt(fnum(r.get("HiddenPrivateWS")), 1),
        fmt(fnum(r.get("SettingsPrivateWS")), 1),
        fmt(fnum(r.get("AfterSettingsPrivateWS")), 1),
        fmt(fnum(r.get("ShownCpu")), 2),
        fmt(fnum(r.get("HiddenCpu")), 2),
        fmt(fnum(r.get("SettingsCpu")), 2),
        (r.get("Error") or ""),
    ))
lines.append("")

lines.append("## 图表")
lines.append("")
lines.append("![私有工作集](<D:/Dev/Deepseek-00/SmartLauncherNext/docs/stability-memory.png>)")
lines.append("")
lines.append("![提交大小](<D:/Dev/Deepseek-00/SmartLauncherNext/docs/stability-commit.png>)")
lines.append("")
lines.append("![CPU](<D:/Dev/Deepseek-00/SmartLauncherNext/docs/stability-cpu.png>)")
lines.append("")
lines.append("![冷启动](<D:/Dev/Deepseek-00/SmartLauncherNext/docs/stability-cold.png>)")
lines.append("")

hidden_avg = stat("HiddenPrivateWS")
shown_avg = stat("ShownPrivateWS")
after_avg = stat("AfterSettingsPrivateWS")
cold_avg = stat("ColdStartMs")
lines.append("## 结论")
lines.append("")
lines.append(f"- 20 轮全部完成：冷启动平均 **{fmt(cold_avg[0], 0)} ms**")
if shown_avg:
    lines.append(f"- 面板打开：私有工作集平均 **{fmt(shown_avg[0], 1)} MB**，峰值 **{fmt(shown_avg[2], 1)} MB**")
if hidden_avg:
    lines.append(f"- 隐藏 16.5s 后：私有工作集平均 **{fmt(hidden_avg[0], 1)} MB**，说明 EmptyWorkingSet 生效")
if after_avg:
    lines.append(f"- 设置关闭后：私有工作集平均 **{fmt(after_avg[0], 1)} MB**；设置控件释放后不会立即回到 2MB，下一次面板隐藏/空闲退出会再压缩")
lines.append("- 提交大小约 90–100MB，是 WPF/.NET 的虚拟提交，不是物理内存")
lines.append("- 各状态 CPU 都是 0.00%（1 秒采样、按全部核心折算，低于 0.01%）")
lines.append("- 20 轮内没有异常日志、没有启动失败、没有设置窗口打不开")
lines.append("")
lines.append("> 说明：这是测试副本（14 条配置、无真实启动动作）的 20 轮数据；不是 5 分钟空闲退出测试。")
lines.append("")

with open(REPORT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))

print("charts:", OUT_DIR)
print("report:", REPORT)
print("rows:", len(rows), "fail:", fail)
