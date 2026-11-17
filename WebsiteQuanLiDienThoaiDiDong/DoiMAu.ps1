param([string]$Theme = "teal")

$f = "Content\Site.css"
if (-not (Test-Path $f)) { Write-Host "Hay chay lenh trong thu muc WebsiteQuanLiDienThoaiDiDong (noi co thu muc Content)"; exit }
$path = (Resolve-Path $f).Path

$themes = @{
  teal   = @{ p='#0f766e'; d='#115e59'; deep='#134e4a'; mid='#115e59'; light='#14b8a6'; acc='#f59e0b'; t1='#f0fdfa'; t2='#ccfbf1'; t3='#99f6e4'; t4='#5eead4'; rgb='15, 118, 110'; drgb='17, 94, 89' }
  blue   = @{ p='#1d4ed8'; d='#1e40af'; deep='#172554'; mid='#1e3a8a'; light='#3b82f6'; acc='#06b6d4'; t1='#f3f7ff'; t2='#dbeafe'; t3='#bfdbfe'; t4='#93c5fd'; rgb='29, 78, 216'; drgb='30, 64, 175' }
  violet = @{ p='#6d28d9'; d='#5b21b6'; deep='#2e1065'; mid='#4c1d95'; light='#8b5cf6'; acc='#ec4899'; t1='#faf5ff'; t2='#ede9fe'; t3='#ddd6fe'; t4='#c4b5fd'; rgb='109, 40, 217'; drgb='91, 33, 182' }
  orange = @{ p='#c2410c'; d='#9a3412'; deep='#431407'; mid='#9a3412'; light='#f97316'; acc='#facc15'; t1='#fff7ed'; t2='#ffedd5'; t3='#fed7aa'; t4='#fdba74'; rgb='194, 65, 12'; drgb='154, 52, 18' }
}
if (-not $themes.ContainsKey($Theme)) { Write-Host "Ten tong khong hop le. Chon: teal, blue, violet, orange"; exit }
$c = $themes[$Theme]

# sao luu ban goc (chi lam lan dau)
$bak = "$path.mau-do.bak"
if (-not (Test-Path $bak)) { Copy-Item $path $bak }

$t = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

# 1. doi ma mau cung thanh bien (neu da doi truoc do thi khong con gi de doi, bo qua)
$t = [regex]::Replace($t, '#fee2e2(?=;\s*color:\s*#b91c1c)', '#fee2e2__ERR', 'IgnoreCase')   # giu do canh bao cho "Da huy"/nhan loi

$map = [ordered]@{
  '#d70018' = 'var(--red)';        '#a80012' = 'var(--red-dark)';   '#b80014' = 'var(--red-dark)'
  '#e51a30' = 'var(--brand-light)'; '#c40014' = 'var(--red)'
  '#b3000f' = 'var(--brand-mid)';   '#b3001b' = 'var(--brand-mid)'
  '#7a0010' = 'var(--brand-deep)';  '#8e000f' = 'var(--brand-deep)'
  '#ff6b6b' = 'var(--brand-light)'; '#ff6b3d' = 'var(--brand-accent)'
  '#fff5f5' = 'var(--brand-tint-1)'; '#fff1f2' = 'var(--brand-tint-1)'; '#fff7f7' = 'var(--brand-tint-1)'; '#fffafa' = 'var(--brand-tint-1)'
  '#fee2e2' = 'var(--brand-tint-2)'; '#ffe3e6' = 'var(--brand-tint-2)'
  '#fecaca' = 'var(--brand-tint-3)'; '#f1d5d5' = 'var(--brand-tint-3)'
  '#fca5a5' = 'var(--brand-tint-4)'
}
foreach ($k in $map.Keys) { $t = [regex]::Replace($t, [regex]::Escape($k), $map[$k], 'IgnoreCase') }
$t = [regex]::Replace($t, 'rgba\(\s*215\s*,\s*0\s*,\s*24\s*,\s*([\d.]+)\s*\)', 'rgba(var(--brand-rgb), $1)')
$t = $t.Replace('#fee2e2__ERR', '#fee2e2')

# 2. ghi bang mau o dau file
$root = @"
:root {
    /* ===== BANG MAU CHU DAO ($Theme) - sua khoi nay de doi mau ca web ===== */
    --red: $($c.p);
    --red-dark: $($c.d);
    --brand-deep: $($c.deep);
    --brand-mid: $($c.mid);
    --brand-light: $($c.light);
    --brand-accent: $($c.acc);
    --brand-tint-1: $($c.t1);
    --brand-tint-2: $($c.t2);
    --brand-tint-3: $($c.t3);
    --brand-tint-4: $($c.t4);
    --brand-rgb: $($c.rgb);
    --brand-dark-rgb: $($c.drgb);
    /* ===== het bang mau ===== */
    --ink: #1f2328;
    --muted: #6b7280;
    --bg: #f3f4f6;
    --line: #e5e7eb;
    --gold: #ffb800;
    --font: 'Be Vietnam Pro','Segoe UI',system-ui,sans-serif;
    --bs-body-font-family: var(--font);
    --bs-link-color-rgb: var(--brand-rgb);
    --bs-link-hover-color-rgb: var(--brand-dark-rgb);
    --bs-primary: var(--red);
    --bs-primary-rgb: var(--brand-rgb);
}
"@
$re = [regex]::new('(?s):root\s*\{.*?\}')
$t = $re.Replace($t, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $root }, 1)

# 3. ghi de Bootstrap cho cung mau (chi them mot lan)
if ($t -notmatch 'BOOTSTRAP OVERRIDE') {
$t += @"

/* ===== BOOTSTRAP OVERRIDE (MAU CHU DAO) ===== */
.btn-outline-danger {
    --bs-btn-color: var(--red); --bs-btn-border-color: var(--red);
    --bs-btn-hover-color: #fff; --bs-btn-hover-bg: var(--red); --bs-btn-hover-border-color: var(--red);
    --bs-btn-focus-shadow-rgb: var(--brand-rgb);
    --bs-btn-active-color: #fff; --bs-btn-active-bg: var(--red-dark); --bs-btn-active-border-color: var(--red-dark);
    --bs-btn-disabled-color: var(--red); --bs-btn-disabled-border-color: var(--red);
}
.btn-danger {
    --bs-btn-color: #fff; --bs-btn-bg: var(--red); --bs-btn-border-color: var(--red);
    --bs-btn-hover-color: #fff; --bs-btn-hover-bg: var(--red-dark); --bs-btn-hover-border-color: var(--red-dark);
    --bs-btn-focus-shadow-rgb: var(--brand-rgb);
    --bs-btn-active-color: #fff; --bs-btn-active-bg: var(--red-dark); --bs-btn-active-border-color: var(--red-dark);
    --bs-btn-disabled-bg: var(--red); --bs-btn-disabled-border-color: var(--red);
}
.text-brand { color: var(--red) !important; }
.form-control:focus, .form-select:focus, .form-check-input:focus {
    border-color: var(--red); box-shadow: 0 0 0 .2rem rgba(var(--brand-rgb), .18);
}
.form-check-input:checked { background-color: var(--red); border-color: var(--red); }
.page-link { color: var(--red); }
.page-item.active .page-link { background: var(--red); border-color: var(--red); color: #fff; }
::selection { background: var(--brand-tint-3); }
"@
}

[IO.File]::WriteAllText($path, $t, (New-Object Text.UTF8Encoding($true)))
Write-Host "Xong. Da ap dung tong '$Theme'. Ban goc luu o: $bak"