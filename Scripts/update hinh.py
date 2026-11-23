# tai_anh_tgdd.py - Tai anh san pham tu thegioididong (gallery chinh thuc, khop san pham)
# pip install requests pillow
import json
import re
import shutil
import time
import unicodedata
from io import BytesIO
from pathlib import Path
from urllib.parse import quote

import requests
from PIL import Image, ImageDraw

HERE = Path(r"C:\Users\ADMIN\Downloads\phone-store-web\WebsiteQuanLiDienThoaiDiDong")
OUT_DIR = HERE / "Content" / "images" / "products"
SO_ANH = 6
PAUSE = 1.5
MAX_SIDE = 1000
HEADERS = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                         "(KHTML, like Gecko) Chrome/124.0 Safari/537.36"}

SAN_PHAM = [
    ("iphone-16-pro-max.jpg", "iPhone 16 Pro Max 256GB", "https://www.thegioididong.com/dtdd/iphone-16-pro-max"),
    ("iphone-16.jpg", "iPhone 16 256GB", "https://www.thegioididong.com/dtdd/iphone-16-256gb"),
    ("iphone-15.jpg", "iPhone 15 128GB", None),
    ("galaxy-s25-ultra.jpg", "Samsung Galaxy S25 Ultra", None),
    ("galaxy-z-flip6.jpg", "Samsung Galaxy Z Flip6", None),
    ("galaxy-a56-5g.jpg", "Samsung Galaxy A56 5G", None),
    ("redmi-note-14-pro-5g.jpg", "Xiaomi Redmi Note 14 Pro 5G", None),
    ("oppo-reno12-f-5g.jpg", "OPPO Reno12 F 5G", None),
    ("vivo-v40-5g.jpg", "vivo V40 5G", None),

    ("airpods-pro-2.jpg", "AirPods Pro 2 USB-C", None),
    ("galaxy-buds3-pro.jpg", "Samsung Galaxy Buds3 Pro", None),
    ("anker-nano-30w.jpg", "Củ sạc Anker Nano 30W", None),
    ("baseus-cap-100w.jpg", "Cáp Baseus USB-C 100W 1m", None),
    ("anker-pin-10000.jpg", "Pin sạc dự phòng Anker 10000mAh", None),
    ("spigen-ip16pm.jpg", "Ốp lưng Spigen iPhone 16 Pro Max", None),
    ("spigen-s25-ultra.jpg", "Ốp lưng Spigen Galaxy S25 Ultra", None),
    ("kinh-cuong-luc-ip16.jpg", "Kính cường lực Spigen iPhone 16", None),
]

CHUYEN_MUC = ["dtdd", "tai-nghe", "sac-cap", "sac-dtdd", "op-lung-flipcover",
              "mieng-dan-man-hinh", "do-choi-dien-thoai", "phu-kien"]
GENERIC = {"op", "lung", "cuong", "luc", "cap", "sac", "pin", "du", "phong", "mieng", "dan", "kinh",
           "usb", "c", "to", "type", "cho", "va", "chinh", "hang", "tai", "nghe", "case", "dung", "luong"}
BIEN_THE = {"pro", "plus", "ultra", "max"}


def bo_dau(text):
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return text.replace("đ", "d").replace("Đ", "D").lower()


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", bo_dau(text)).strip("-")


def tokens_chinh(key):
    return [t for t in re.split(r"[^a-z0-9]+", bo_dau(key))
            if t and not re.match(r"^\d+(gb|tb)$", t) and t not in GENERIC]



def ung_vien_url(key):
    ten = re.sub(r"\b\d+\s?(GB|TB)\b", "", key, flags=re.I).strip()
    s = slug(ten)
    ds = [s]
    m = re.search(r"(\d+)\s?(GB|TB)", key, flags=re.I)
    if m:
        ds.insert(0, f"{s}-{m.group(1)}{m.group(2).lower()}")
    return [f"https://www.thegioididong.com/{cm}/{d}" for cm in CHUYEN_MUC for d in ds]


def kiem_tra_trang(url, key):
    r = requests.get(url, headers=HEADERS, timeout=20)
    if r.status_code != 200:
        return None
    html = r.text
    m = re.search(r"<title>(.*?)</title>", html, flags=re.S | re.I)
    tieu_de = m.group(1) if m else ""
    tu_trang = set(re.split(r"[^a-z0-9]+", bo_dau(tieu_de)))
    tokens = tokens_chinh(key)
    if not all(t in tu_trang for t in tokens):
        return None
    if any(w in tu_trang for w in BIEN_THE if w not in tokens):
        return None
    return html


def tim_link_bang_tim_kiem(key):
    url = "https://www.thegioididong.com/tim-kiem?key=" + quote(key)
    html = requests.get(url, headers=HEADERS, timeout=20).text
    tokens = tokens_chinh(key)
    for path in re.findall(r'href="((?:https://www\.thegioididong\.com)?/[a-z0-9\-]+/[a-z0-9\-]+)"', html):
        slug_path = path.rstrip("/").rsplit("/", 1)[-1].lower()

        if all(t in slug_path for t in tokens):
            full = path if path.startswith("http") else "https://www.thegioididong.com" + path
            try:
                if kiem_tra_trang(full, key):
                    return full
            except Exception:
                pass
    return None


def tim_trang(key, direct):
    if direct:
        return direct, requests.get(direct, headers=HEADERS, timeout=20).text
    for url in ung_vien_url(key):
        try:
            html = kiem_tra_trang(url, key)
        except Exception:
            html = None
        time.sleep(0.5)
        if html:
            return url, html
    link = tim_link_bang_tim_kiem(key)
    if link:
        return link, requests.get(link, headers=HEADERS, timeout=20).text
    return None, None


def lay_anh_gallery(html):
    anh, da_thay = [], set()
    for m in re.finditer(r'https://cdnv2\.tgdd\.vn/mwg-static/tgdd/Products/Images/[^"\'\s<>)]+?\.(?:jpg|jpeg|png|webp)',
                         html, flags=re.I):
        u = m.group(0)

        ten = u.rsplit("/", 1)[-1]
        if ten in da_thay:
            continue
        da_thay.add(ten)
        anh.append(u)
    return anh


def tai_anh(url):
    try:
        r = requests.get(url, headers=HEADERS, timeout=20)
        r.raise_for_status()
        img = Image.open(BytesIO(r.content))
        img.load()
        if img.mode in ("RGBA", "LA", "P"):
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


def tao_anh_tam(duong_dan, chu):
    img = Image.new("RGB", (600, 600), (235, 238, 245))
    ImageDraw.Draw(img).text((20, 300), "Chua co anh: " + chu, fill=(90, 100, 120))
    img.save(duong_dan, "JPEG", quality=90)



def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    sao_luu = OUT_DIR.parent / "products_cu"
    if not sao_luu.exists():
        shutil.copytree(OUT_DIR, sao_luu)
        print("Da sao luu anh cu vao:", sao_luu)

    bao_cao = {"thanh_cong": {}, "that_bai": {}}
    for file, key, direct in SAN_PHAM:
        base = file[:-4]
        print(f"\n[{key}]")
        try:
            link, html = tim_trang(key, direct)
        except Exception as e:
            link, html = None, None
            print("   loi khi tim trang:", e)
        if not link:
            print("   khong tim thay trang san pham -> giu anh cu")
            bao_cao["that_bai"][key] = "khong tim thay trang san pham"
            continue
        print("   trang:", link)
        urls = lay_anh_gallery(html)[:SO_ANH]
        if len(urls) < 2:
            print("   khong lay duoc gallery -> giu anh cu")
            bao_cao["that_bai"][key] = "khong lay duoc gallery"
            continue

        luu = []
        for i, u in enumerate(urls, start=1):
            ten_file = file if i == 1 else f"{base}-{i}.jpg"
            img = tai_anh(u)

            if img is None:
                continue
            img.save(OUT_DIR / ten_file, "JPEG", quality=90)
            luu.append(ten_file)
            time.sleep(0.3)
        bao_cao["thanh_cong"][key] = {"trang": link, "file": luu}
        print("   da luu:", ", ".join(luu))

        for k in range(2, SO_ANH + 1):
            ten_file = f"{base}-{k}.jpg"
            if ten_file not in luu and not (OUT_DIR / ten_file).exists():
                tao_anh_tam(OUT_DIR / ten_file, ten_file)
        time.sleep(PAUSE)

    (HERE / "bao_cao_tgdd.json").write_text(json.dumps(bao_cao, ensure_ascii=False, indent=2), encoding="utf-8")
    print("\n===== XONG =====")
    print("Thanh cong:", len(bao_cao["thanh_cong"]), "| That bai:", len(bao_cao["that_bai"]))
    print("Chi tiet:", HERE / "bao_cao_tgdd.json")


if __name__ == "__main__":
    main()