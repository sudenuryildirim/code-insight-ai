# CodeInsightAI

Yapay zeka destekli GitHub pull request inceleme aracı. Yapılandırılan bir veya birden fazla GitHub reposundaki açık
pull request'leri listeler (repo seçici ile aralarında geçiş yapılır); her biri için PR'ın amacını, olası bug/mantık
hatalarını, güvenlik açıklarını (OWASP Top 10) ve projenin geri kalanıyla tutarlılığını değerlendiren detaylı bir
rapor üretir, indirilebilir bir PDF olarak sunar.

Bu modül **hiçbir zaman otomatik approve/merge yapmaz** — nihai karar her zaman GitHub üzerinde insan tarafından verilir.

## Proje yapısı

- `backend/` - ASP.NET Core 9 (Clean Architecture: Domain / Application / Infrastructure / API), GitHub REST API'den
  PR diff'lerini çeker, Google Gemini ile analiz eder, QuestPDF ile PDF rapor üretir. İncelemeler SQLite'a
  (`codeinsight.db`) kaydedilir; aynı commit için tekrar inceleme istenirse Gemini'ye gitmeden anında sonuç döner.
  `CodeInsightAI.Tests` (xUnit) birim testlerini içerir.
- `frontend-angular/` - Angular 18 arayüzü (açık PR listesi, inceleme raporu görüntüleme, geçmiş incelemeler,
  PDF indirme). SignalR üzerinden backend'e bağlanır; GitHub webhook'u bir PR'ı güncellediğinde liste otomatik
  yenilenir (bkz. "Gerçek zamanlı bildirim" altında).
- `frontend/` - önceki React/Vite arayüzü (artık kullanılmıyor, Angular ile değiştirildi).

## Çalıştırma

Backend:

```bash
cd backend/CodeInsightAI.API
dotnet restore
dotnet run
```

API varsayılan olarak `http://localhost:5228` adresinde çalışır. Gemini API anahtarı `appsettings.json` /
`appsettings.Development.json` içindeki `Gemini:ApiKey` alanından okunur.

PR inceleme özelliğini kullanmak için `appsettings.Development.json` içine kendi değerlerinizi girin
(bu dosya `.gitignore`'da olduğu için commit edilmez):

```json
"GitHub": {
  "Token": "<organizasyondaki repolara okuma/yorum yazma yetkisine sahip bir Personal Access Token>",
  "Organization": "<organizasyon adı, ör. acme-inc>",
  "ApiBaseUrl": "<opsiyonel - self-hosted GitHub Enterprise Server kullanıyorsanız ör. https://git.sirket.com/api/v3/, boş bırakılırsa public GitHub (api.github.com) kullanılır>",
  "WebhookSecret": "<GitHub webhook ayarlarında girdiğiniz secret ile aynı olmalı>"
}
```

`Organization` altındaki tüm repolar `GET /orgs/{org}/repos` ile otomatik keşfedilir — elle repo listesi girmeye
gerek yoktur; arayüzde birden fazla repo bulunduğunda üstte bir repo seçici (chip listesi) belirir. Tüm repolar
aynı `Token`'ı kullanır, bu yüzden token'ın erişebildiği bir hesap/organizasyon token'ı kullanın.

PR listesi ve inceleme her zaman GitHub API'den canlı çekilir; webhook (`POST /api/github/webhook`) bu yüzden
listenin çalışması için gerekli değildir — sadece isteğe bağlı bir hızlandırma katmanıdır (local geliştirmede
GitHub'ın webhook'a ulaşabilmesi için ngrok gibi bir tünel gerekir).

### Gerçek zamanlı bildirim (SignalR)

Backend `/hubs/pull-requests` adresinde bir SignalR hub'ı yayınlar. Arayüz açılışta buna bağlanır ve üstte
"Canlı" / "Bağlanıyor…" göstergesi gösterir. GitHub webhook'u imzası doğrulanmış bir `pull_request` event'i
(`opened`/`synchronize`/`reopened`) aldığında, bu hub üzerinden tüm bağlı istemcilere bir bildirim yayınlar;
arayüz bunu alınca (o an bir rapor görüntülenmiyorsa) PR listesini otomatik yeniler — elle "Yenile"ye
basmaya gerek kalmaz. Test suite'inin webhook imza doğrulama testleri dışında, bu akışı local'de test etmek
için GitHub'ın gerçek webhook çağrısı yerine imzalı bir `curl` isteği `POST /api/github/webhook`'a
gönderilebilir.

Frontend (Angular):

```bash
cd frontend-angular
npm install
npm start
```

Arayüz `http://localhost:4200` adresinde açılır. Ayrıntılar için `frontend-angular/README.md` dosyasına bakın.
