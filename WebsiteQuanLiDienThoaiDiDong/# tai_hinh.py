# tai_hinh.py - Tai hinh san pham cho do an PhoneStore
# pip install ddgs requests pillow
import re
import time
from io import BytesIO
from pathlib import Path

import requests
from PIL import Image, ImageDraw

try:
    from ddgs import DDGS
except ImportError:                      # ten cu cua thu vien
    from duckduckgo_search import DDGS

# ====== SUA 2 DONG NAY ======
SQL_FILE = Path(__file__).parent / "QL_DienThoai.sql"
OUT_DIR = Path(r"D:\LapTrinhWeb\phone-store-web\WebsiteQuanLiDienThoaiDiDong\Content\images\products")
# ============================

# ten file (bo hau to -2, -3) -> tu khoa tim kiem
QUERIES = {
    "iphone-16-pro-max.jpg": "iPhone 16 Pro Max",
    "iphone-16.jpg": "iPhone 16",
    "iphone-15.jpg": "iPhone 15",
    "galaxy-s25-ultra.jpg": "Samsung Galaxy S25 Ultra",
    "galaxy-z-flip6.jpg": "Samsung Galaxy Z Flip6",
    "galaxy-a56-5g.jpg": "Samsung Galaxy A56 5G",
    "redmi-note-14-pro-5g.jpg": "Xiaomi Redmi Note 14 Pro 5G",
    "oppo-reno12-f-5g.jpg": "OPPO Reno12 F 5G",
    "vivo-v40-5g.jpg": "vivo V40 5G",
    "airpods-pro-2.jpg": "AirPods Pro 2 USB-C",
    "galaxy-buds3-pro.jpg": "Samsung Galaxy Buds3 Pro",
    "anker-nano-30w.jpg": "Anker Nano 30W USB-C charger",
    "baseus-cap-100w.jpg": "Baseus USB-C to USB-C cable 100W 1m",
    "anker-pin-10000.jpg": "Anker power bank 10000mAh 22.5W",
    "spigen-ip16pm.jpg": "Spigen Liquid Air iPhone 16 Pro Max case",
    "spigen-s25-ultra.jpg": "Spigen Ultra Hybrid Galaxy S25 Ultra case",
    "kinh-cuong-luc-ip16.jpg": "Spigen GlasTR EZ Fit iPhone 16 screen protector",
}

HEADERS = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                         "(KHTML, like Gecko) Chrome/124.0 Safari/537.36"}
MIN_SIZE = 300      # bo anh nho hon 300px
MAX_SIDE = 1000     # thu nho anh lon cho nhe web


def doc_danh_sach_file():
    text = SQL_FILE.read_text(encoding="utf-8-sig")
    names = re.findall(r"/Content/images/products/([^']+\.jpg)", text)
    return sorted(set(names))


def tim_url(query, so_luong=20):
    for lan in range(3):
        try:
            kq = DDGS().images(query, max_results=so_luong)
            return [r["image"] for r in kq if r.get("image")]
        except Exception as e:
            print(f"   (tim kiem loi: {e}, thu lai...)")
            time.sleep(3)
    return []


def tai_anh(url):
    try:
        r = requests.get(url, headers=HEADERS, timeout=15)
        r.raise_for_status()
        img = Image.open(BytesIO(r.content))
        img.load()
        if min(img.size) < MIN_SIZE:
            return None
        if img.mode in ("RGBA", "LA", "P"):          # nen trang cho anh PNG trong suot
            img = img.convert("RGBA")
            nen = Image.new("RGB", img.size, (255, 255, 255))
            nen.paste(img, mask=img.split()[-1])
            img = nen
        else:
            img = img.convert("RGB")
        img.thumbnail((MAX_SIDE, MAX_SIDE))
        return img
    except Exception:
        return None


def tao_anh_tam(duong_dan, chu, kich_thuoc=(600, 600)):
    img = Image.new("RGB", kich_thuoc, (235, 238, 245))
    d = ImageDraw.Draw(img)
    d.text((20, kich_thuoc[1] // 2), chu, fill=(90, 100, 120))
    img.save(duong_dan, "JPEG", quality=90)


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    files = doc_danh_sach_file()
    print(f"Can tai {len(files)} file hinh vao: {OUT_DIR}\n")

    da_dung = {}     # base -> tap URL da dung (de anh -2, -3 khong trung anh chinh)
    loi = []

    # xu ly anh chinh truoc, anh -2/-3 sau
    files.sort(key=lambda n: (bool(re.search(r"-\d\.jpg$", n)), n))

    for name in files:
        dich = OUT_DIR / name
        if dich.exists():
            print(f"[bo qua] {name} da co")
            continue

        base = re.sub(r"-\d\.jpg$", ".jpg", name)
        query = QUERIES.get(base)
        if not query:
            print(f"[?] {name}: chua co tu khoa trong QUERIES")
            tao_anh_tam(dich, name)
            loi.append(name)
            continue

        goc = base == name
        tu_khoa = query + (" official product photo" if goc else " back side angle")
        print(f"[tim] {name}  <-  '{tu_khoa}'")

        urls = tim_url(tu_khoa)
        dung = da_dung.setdefault(base, set())
        ok = False
        for u in urls:
            if u in dung:
                continue
            img = tai_anh(u)
            if img is not None:
                img.save(dich, "JPEG", quality=90)
                dung.add(u)
                print(f"      OK  {u[:80]}")
                ok = True
                break
        if not ok:
            print("      THAT BAI -> tao anh tam")
            tao_anh_tam(dich, name)
            loi.append(name)
        time.sleep(1.5)                  # tranh bi chan vi goi qua nhanh

    # anh du phong cho the <img onerror>
    no_img = OUT_DIR.parent / "no-image.png"
    if not no_img.exists():
        Image.new("RGB", (600, 600), (235, 238, 245)).save(no_img)
        print(f"\nDa tao {no_img}")

    print("\n===== XONG =====")
    if loi:
        print("Hinh phai thay thu cong:", ", ".join(loi))


if __name__ == "__main__":
    main()