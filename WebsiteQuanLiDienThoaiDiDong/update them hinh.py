# tai_anh_tgdd.py - Chi cap nhat anh phu (-2..-6). Anh bia giu nguyen ban goc.
# In tien do tren man hinh, chi tiet ghi vao log_tai_anh.txt
# pip install ddgs requests pillow
import json
import re
import shutil
import time
import unicodedata
from io import BytesIO
from pathlib import Path
from urllib.parse import quote

import requests
from PIL import Image

try:
    from ddgs import DDGS
except ImportError:
    from duckduckgo_search import DDGS

HERE = Path(r"C:\Users\ADMIN\Downloads\phone-store-web\WebsiteQuanLiDienThoaiDiDong")
OUT_DIR = HERE / "Content" / "images" / "products"
CU_DIR = OUT_DIR.parent / "products_cu"
LOG_FILE = HERE / "log_tai_anh.txt"
SO_ANH_PHU = 5
PAUSE = 1.5
MAX_SIDE = 1000
HEADERS = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                         "(KHTML, like Gecko) Chrome/124.0 Safari/537.36"}

SAN_PHAM = [
    ("iphone-16-pro-max.jpg", "iPhone 16 Pro Max 256GB", "https://www.thegioididong.com/dtdd/iphone-16-pro-max", r"16\s*pro\s*max"),

    ("iphone-16.jpg", "iPhone 16 256GB", "https://www.thegioididong.com/dtdd/iphone-16-256gb", r"iphone\s*16(?!\s*(pro|plus|e)\b)"),
    ("iphone-15.jpg", "iPhone 15 128GB", "https://www.thegioididong.com/dtdd/iphone-15", r"iphone\s*15(?!\s*(pro|plus))"),
    ("galaxy-s25-ultra.jpg", "Samsung Galaxy S25 Ultra", "https://www.thegioididong.com/dtdd/samsung-galaxy-s25-ultra", r"s25\s*ultra"),
    ("galaxy-z-flip6.jpg", "Samsung Galaxy Z Flip6", "https://www.thegioididong.com/dtdd/samsung-galaxy-z-flip6", r"flip\s*6"),
    ("galaxy-a56-5g.jpg", "Samsung Galaxy A56 5G", "https://www.thegioididong.com/dtdd/samsung-galaxy-a56", r"a56"),
    ("redmi-note-14-pro-5g.jpg", "Xiaomi Redmi Note 14 Pro 5G", "https://www.thegioididong.com/dtdd/xiaomi-redmi-note-14-pro-5g", r"redmi\s*note\s*14\s*pro"),
    ("oppo-reno12-f-5g.jpg", "OPPO Reno12 F 5G", "https://www.thegioididong.com/dtdd/oppo-reno12-f-5g-8gb-256gb", r"reno\s*12\s*f"),
    ("vivo-v40-5g.jpg", "vivo V40 5G", "https://www.thegioididong.com/dtdd/vivo-v40-5g", r"v40"),
    ("airpods-pro-2.jpg", "AirPods Pro 2 USB-C", "https://www.thegioididong.com/tai-nghe/tai-nghe-bluetooth-airpods-pro-gen-2-magsafe-charge-usb-c-apple-mtjv3", r"airpods\s*pro\s*2"),
    ("galaxy-buds3-pro.jpg", "Samsung Galaxy Buds3 Pro", "https://www.thegioididong.com/tai-nghe/tai-nghe-bluetooth-true-wireless-samsung-galaxy-buds-3-pro-r630n", r"buds\s*3\s*pro"),
    ("anker-nano-30w.jpg", "Củ sạc Anker Nano 30W", None, r"nano|30\s*w"),
    ("baseus-cap-100w.jpg", "Cáp Baseus USB-C 100W 1m", None, r"100\s*w|baseus"),
    ("anker-pin-10000.jpg", "Pin sạc dự phòng Anker 10000mAh", None, r"10000|10\s*000|10k|anker"),
    ("spigen-ip16pm.jpg", "Ốp lưng Spigen iPhone 16 Pro Max", None, r"16\s*pro\s*max|spigen"),
    ("spigen-s25-ultra.jpg", "Ốp lưng Spigen Galaxy S25 Ultra", None, r"s25\s*ultra|spigen"),
    ("kinh-cuong-luc-ip16.jpg", "Kính cường lực Spigen iPhone 16", None, r"16|spigen"),
]

CHUYEN_MUC = ["dtdd", "tai-nghe", "sac-cap", "sac-dtdd", "op-lung-flipcover",
              "mieng-dan-man-hinh", "do-choi-dien-thoai", "phu-kien"]
GENERIC = {"op", "lung", "cuong", "luc", "cap", "sac", "pin", "du", "phong", "mieng", "dan", "kinh",
           "usb", "c", "to", "type", "cho", "va", "chinh", "hang", "tai", "nghe", "case", "dung", "luong"}
BIEN_THE = {"pro", "plus", "ultra", "max"}
UU_TIEN = ["thegioididong", "cellphones", "fptshop", "dienmayxanh", "hoanghamobile", "24hstore",
           "samsung", "apple", "oppo", "vivo", "xiaomi", "spigen", "anker", "baseus"]
CAU_TIM_MANG = ["{n}", "{n} mặt sau", "{n} góc nghiêng", "{n} camera", "{n} chính hãng", "{n} side view"]

REGEX_ANH = re.compile(
    r'https://cdnv2\.tgdd\.vn/mwg-static/tgdd/Products/Images/[^"\'\s<>)]+?\.(?:jpg|jpeg|png|webp)',
    flags=re.I)



def log(*args):
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(" ".join(str(a) for a in args) + "\n")


def bo_dau(text):
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return text.replace("đ", "d").replace("Đ", "D").lower()


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", bo_dau(text)).strip("-")


def tokens_chinh(key):
    return [t for t in re.split(r"[^a-z0-9]+", bo_dau(key))
            if t and not re.match(r"^\d+(gb|tb)$", t) and t not in GENERIC]


def ten_anh_phu(file, k):
    return f"{file[:-4]}-{k}.jpg"


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
    for m in REGEX_ANH.finditer(html):
        u = m.group(0)
        ten = u.rsplit("/", 1)[-1].lower()
        if ten in da_thay or "slider" in u.lower() or "banner" in u.lower():
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
        if min(img.size) < 300:
            return None
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


def tim_anh_mang(query, regex, da_dung):
    kq = []
    for _ in range(3):
        try:
            kq = DDGS().images(query, max_results=30)
            break
        except Exception as e:
            log("   loi tim anh:", e)
            time.sleep(3)
    ds = []
    for r in kq:
        url = r.get("image")

        tieu_de = (r.get("title") or "").lower()
        nguon = ((r.get("url") or "") + " " + (r.get("source") or "")).lower()
        if not url or url in da_dung:
            continue
        if not re.search(regex, tieu_de, flags=re.I):
            continue
        diem = next((i for i, k in enumerate(UU_TIEN) if k in nguon), len(UU_TIEN))
        ds.append((diem, url))
    ds.sort()
    return [u for _, u in ds]


def bo_sung_tu_mang(file, ten, regex, da_luu):
    con_thieu = [k for k in range(2, SO_ANH_PHU + 2) if ten_anh_phu(file, k) not in da_luu]
    luu_them, da_dung = [], set()
    for cau in CAU_TIM_MANG:
        if not con_thieu:
            break
        query = cau.format(n=ten)
        log("   [tim tren mang]", query)
        for url in tim_anh_mang(query, regex, da_dung):
            if not con_thieu:
                break
            da_dung.add(url)
            img = tai_anh(url)
            if img is None:
                continue
            k = con_thieu.pop(0)
            ten_file = ten_anh_phu(file, k)
            img.save(OUT_DIR / ten_file, "JPEG", quality=90)
            luu_them.append(ten_file)
            log("      OK ->", ten_file, "|", url[:80])

            time.sleep(0.3)
        time.sleep(PAUSE)
    return luu_them


def dien_anh_thieu(sql_path):
    s = sql_path.read_text(encoding="utf-8-sig")
    names = sorted(set(re.findall(r"/Content/images/products/([^'\s]+\.jpg)", s)))
    for n in names:
        dich = OUT_DIR / n
        if dich.exists():
            continue
        if (CU_DIR / n).exists():
            shutil.copy(CU_DIR / n, dich)
            log("copy anh cu:", n)
            continue
        bia = re.sub(r"-\d\.jpg$", ".jpg", n)
        if (OUT_DIR / bia).exists():
            shutil.copy(OUT_DIR / bia, dich)
            log("copy anh bia tam:", n)
        else:
            log("KHONG CO ANH:", n)


def main():
    LOG_FILE.write_text("", encoding="utf-8")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    if not CU_DIR.exists():
        shutil.copytree(OUT_DIR, CU_DIR)
        log("Da sao luu anh cu vao:", CU_DIR)

    for file, _, _, _ in SAN_PHAM:

        if (CU_DIR / file).exists():
            shutil.copy(CU_DIR / file, OUT_DIR / file)
    log("Khoi phuc anh bia ve ban goc")

    bao_cao = {"tgdd": {}, "tu_mang": {}, "that_bai": {}}
    tong = len(SAN_PHAM)
    for stt, (file, key, direct, regex) in enumerate(SAN_PHAM, start=1):
        print(f"[{stt}/{tong}] {key} ...", flush=True)
        log(f"\n[{key}]")
        da_luu = []
        try:
            link, html = tim_trang(key, direct)
        except Exception as e:
            link, html = None, None
            log("   loi khi tim trang:", e)

        if link and html:
            log("   trang:", link)
            urls = lay_anh_gallery(html)[1:1 + SO_ANH_PHU]
            for k, u in enumerate(urls, start=2):
                img = tai_anh(u)
                if img is None:
                    continue
                ten_file = ten_anh_phu(file, k)
                img.save(OUT_DIR / ten_file, "JPEG", quality=90)
                da_luu.append(ten_file)
                time.sleep(0.3)
            if da_luu:
                bao_cao["tgdd"][key] = {"trang": link, "file": da_luu}
                log("   tgdd da luu:", ", ".join(da_luu))
            else:
                log("   tgdd khong co gallery")

        else:
            log("   khong tim thay trang tgdd")

        them = []
        if len(da_luu) < SO_ANH_PHU:
            them = bo_sung_tu_mang(file, key, regex, da_luu)
            if them:
                bao_cao["tu_mang"][key] = them
        ket = len(da_luu) + len(them)
        if ket < SO_ANH_PHU:
            bao_cao["that_bai"][key] = f"thieu {SO_ANH_PHU - ket} anh phu"
        print(f"   -> {ket}/{SO_ANH_PHU} anh phu", flush=True)
        time.sleep(PAUSE)

    log("\n===== DIEN ANH THIEU =====")
    dien_anh_thieu(HERE / "QL_DienThoai.sql")

    (HERE / "bao_cao_tgdd.json").write_text(json.dumps(bao_cao, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"XONG. TGDD: {len(bao_cao['tgdd'])} | Tu mang: {len(bao_cao['tu_mang'])} | Van thieu: {len(bao_cao['that_bai'])}")


if __name__ == "__main__":
    main()