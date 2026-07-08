# CodeInsightAI - Angular Arayüzü

Bu klasör, backend'in (`../backend`) sunduğu kod inceleme API'sini kullanan Angular arayüzüdür.
Programlama dili seçimi yoktur; dil, yapay zeka tarafından dosya adına ve kod söz dizimine bakılarak otomatik tespit edilir.

## Kurulum

```bash
cd frontend-angular
npm install
```

## Çalıştırma

1. Backend'i çalıştırın (varsayılan: `http://localhost:5228`):
   ```bash
   cd ../backend/CodeInsightAI.API
   dotnet restore
   dotnet run
   ```
2. Angular geliştirme sunucusunu başlatın:
   ```bash
   npm start
   ```
3. Tarayıcıda `http://localhost:4200` adresini açın.

Backend farklı bir adres/portta çalışıyorsa `src/app/services/code-review.service.ts` içindeki `baseUrl` değerini güncelleyin.

## Notlar

- "PDF Olarak İndir" butonu, backend'deki `/api/codereview/report/pdf` uç noktasını çağırır; bu, ekranda gösterilen raporu tekrar yapay zekaya sormadan PDF'e dönüştürür.
- Backend CORS ayarı (`Program.cs`) sadece `http://localhost:4200` origin'ine izin verir; farklı bir port kullanırsanız orayı da güncelleyin.
