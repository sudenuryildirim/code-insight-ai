# CodeInsightAI

Yapay zeka destekli GitHub pull request inceleme aracı. Bağlı bir GitHub reposundaki açık pull request'leri listeler;
her biri için PR'ın amacını, olası bug/mantık hatalarını, güvenlik açıklarını (OWASP Top 10) ve projenin geri kalanıyla
tutarlılığını değerlendiren detaylı bir rapor üretir, indirilebilir bir PDF olarak sunar.

Bu modül **hiçbir zaman otomatik approve/merge yapmaz** — nihai karar her zaman GitHub üzerinde insan tarafından verilir.

## Proje yapısı

- `backend/` - ASP.NET Core 9 (Clean Architecture: Domain / Application / Infrastructure / API), GitHub REST API'den
  PR diff'lerini çeker, Google Gemini ile analiz eder, QuestPDF ile PDF rapor üretir.
- `frontend-angular/` - Angular 18 arayüzü (açık PR listesi, inceleme raporu görüntüleme, PDF indirme).
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
  "Token": "<repo okuma yetkisine sahip bir GitHub Personal Access Token>",
  "Owner": "<repo sahibi/organizasyon adı>",
  "Repo": "<repo adı>",
  "WebhookSecret": "<GitHub webhook ayarlarında girdiğiniz secret ile aynı olmalı>"
}
```

PR listesi ve inceleme her zaman GitHub API'den canlı çekilir; webhook (`POST /api/github/webhook`) sadece
imza doğrulaması yapıp event'i kabul eder, ileride gerçek zamanlı bildirim için kullanılabilir ama bugün
listenin çalışması için gerekli değildir (local geliştirmede GitHub'ın webhook'a ulaşabilmesi için ngrok gibi
bir tünel gerekir).

Frontend (Angular):

```bash
cd frontend-angular
npm install
npm start
```

Arayüz `http://localhost:4200` adresinde açılır. Ayrıntılar için `frontend-angular/README.md` dosyasına bakın.
