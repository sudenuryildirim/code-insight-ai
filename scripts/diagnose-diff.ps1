# PR diff'inin GitHub'dan neden gelmediğini teşhis eder.
#
# Uygulamanın GitHubService'in denediği DÖRT yolu tek tek çağırır ve her birinin
# HTTP durumunu + dönen içeriğin ilk satırlarını ham olarak yazar. Böylece sorunun
# "GitHub diff'i hiç vermiyor" mu yoksa "uygulama yanlış çekiyor" mu olduğu netleşir.
#
# Token'ı git'e girmemek için appsettings.Development.json'dan okur (o dosya .gitignore'da).
#
# Kullanım (git.johnsonelectric.com'a erişebilen makinede, repo kökünden):
#   powershell -ExecutionPolicy Bypass -File scripts/diagnose-diff.ps1 -Owner <sahip> -Repo <repo> -PrNumber 21
#
# Owner ve Repo'yu PR'ın tarayıcıdaki adresinden okuyabilirsin:
#   https://git.johnsonelectric.com/<Owner>/<Repo>/pull/21   ->  buradaki <Owner> ve <Repo>

param(
    [Parameter(Mandatory = $true)][string]$Owner,
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][int]$PrNumber
)

$ErrorActionPreference = 'Stop'

$appsettingsPath = Join-Path $PSScriptRoot '..\backend\CodeInsightAI.API\appsettings.Development.json'
if (-not (Test-Path $appsettingsPath)) {
    Write-Host "appsettings.Development.json bulunamadı: $appsettingsPath" -ForegroundColor Red
    Write-Host "Bu dosyayı GitHub:Token ve GitHub:ApiBaseUrl ile oluşturmalısın." -ForegroundColor Red
    exit 1
}

$cfg = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$token = $cfg.GitHub.Token
$apiBase = $cfg.GitHub.ApiBaseUrl.TrimEnd('/')          # örn. https://git.johnsonelectric.com/api/v3
$webBase = $apiBase -replace '/api/v3$', ''             # örn. https://git.johnsonelectric.com

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "GitHub:Token boş. appsettings.Development.json içine token gir." -ForegroundColor Red
    exit 1
}

Write-Host "API taban: $apiBase"
Write-Host "Web taban: $webBase"
Write-Host "PR: $Owner/$Repo #$PrNumber"

# curl.exe her HTTP durumunu (hata dahil) sorunsuz döndürdüğü ve PowerShell sürüm
# farklarından etkilenmediği için Invoke-WebRequest yerine onu kullanıyoruz.
function Test-Url {
    param([string]$Title, [string]$Url, [string]$Accept)

    Write-Host ""
    Write-Host "==== $Title ====" -ForegroundColor Cyan
    Write-Host "URL: $Url"

    $body = & curl.exe -s -w "`n---META---`nHTTP:%{http_code}`nTYPE:%{content_type}`n" `
        -H "Authorization: Bearer $token" `
        -H "User-Agent: CodeInsightAI-Diag" `
        -H "Accept: $Accept" `
        "$Url"

    $parts = $body -split "`n---META---`n"
    $content = $parts[0]
    $meta = if ($parts.Count -gt 1) { $parts[1] } else { "" }

    Write-Host $meta.Trim()
    Write-Host ("Uzunluk: {0} karakter" -f $content.Length)

    $preview = if ($content.Length -gt 600) { $content.Substring(0, 600) } else { $content }
    Write-Host "İçeriğin ilk kısmı:" -ForegroundColor Yellow
    Write-Host $preview

    if ($content.TrimStart().StartsWith("diff --git")) {
        Write-Host ">>> BU GERÇEK BİR DIFF. Bu yol çalışıyor." -ForegroundColor Green
    }
    elseif ($content.TrimStart().StartsWith("<")) {
        Write-Host ">>> HTML/XML döndü (muhtemelen login sayfası ya da hata). Diff değil." -ForegroundColor Red
    }
}

# 1) PR endpoint'i, diff media type ile (uygulamanın ilk denediği yol)
Test-Url "1) PR endpoint (.diff media type)" "$apiBase/repos/$Owner/$Repo/pulls/$PrNumber" "application/vnd.github.diff"

# 2) files endpoint'i (JSON) — patch alanları null mu geliyor?
Write-Host ""
Write-Host "==== 2) files endpoint (patch alanı null mu?) ====" -ForegroundColor Cyan
$filesUrl = "$apiBase/repos/$Owner/$Repo/pulls/$PrNumber/files?per_page=100"
Write-Host "URL: $filesUrl"
$filesJson = & curl.exe -s -H "Authorization: Bearer $token" -H "User-Agent: CodeInsightAI-Diag" -H "Accept: application/vnd.github+json" "$filesUrl"
try {
    $files = $filesJson | ConvertFrom-Json
    Write-Host ("Dosya sayısı: {0}" -f $files.Count)
    $withPatch = ($files | Where-Object { $_.patch }).Count
    $nullPatch = $files.Count - $withPatch
    Write-Host ("patch DOLU olan dosya: {0}" -f $withPatch)
    Write-Host ("patch NULL olan dosya: {0}" -f $nullPatch) -ForegroundColor Yellow
    if ($nullPatch -eq $files.Count -and $files.Count -gt 0) {
        Write-Host ">>> TÜM patch alanları null. GitHub bu PR için dosya-bazlı diff'i kesmiş; ham diff yollarına bağımlıyız." -ForegroundColor Red
    }
}
catch {
    Write-Host "files JSON ayrıştırılamadı. Ham cevabın ilk kısmı:" -ForegroundColor Red
    $p = if ($filesJson.Length -gt 400) { $filesJson.Substring(0, 400) } else { $filesJson }
    Write-Host $p
}

# 3) compare endpoint'i — base ve head SHA'yı önce PR JSON'undan al
Write-Host ""
Write-Host "==== 3) compare endpoint (base...head, .diff) ====" -ForegroundColor Cyan
$prJson = & curl.exe -s -H "Authorization: Bearer $token" -H "User-Agent: CodeInsightAI-Diag" -H "Accept: application/vnd.github+json" "$apiBase/repos/$Owner/$Repo/pulls/$PrNumber"
try {
    $pr = $prJson | ConvertFrom-Json
    $baseSha = $pr.base.sha
    $headSha = $pr.head.sha
    Write-Host "base sha: $baseSha"
    Write-Host "head sha: $headSha"
    Test-Url "3b) compare $baseSha...$headSha" "$apiBase/repos/$Owner/$Repo/compare/$baseSha...$headSha" "application/vnd.github.diff"
}
catch {
    Write-Host "PR JSON ayrıştırılamadı, compare denenemedi." -ForegroundColor Red
}

# 4) Web arayüzünün .diff indirme URL'i (tarayıcıdan indirdiğin dosyayla aynı yol)
Test-Url "4) Web .diff URL" "$webBase/$Owner/$Repo/pull/$PrNumber.diff" "application/vnd.github.diff"

Write-Host ""
Write-Host "==== ÖZET ====" -ForegroundColor Cyan
Write-Host "Yukarıda 'BU GERÇEK BİR DIFF' yazan yeşil satır varsa, o yol çalışıyor demektir."
Write-Host "Hiçbiri yeşil değilse GitHub bu token'a bu PR'ın diff'ini hiçbir yoldan vermiyor - o zaman"
Write-Host "sorun token yetkisi/GHES yapılandırması tarafında, uygulama kodunda değil."
