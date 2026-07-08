import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { CodeInputComponent } from './components/code-input/code-input.component';
import { ReportViewComponent } from './components/report-view/report-view.component';
import { PullRequestListComponent } from './components/pull-request-list/pull-request-list.component';
import { CodeReviewService } from './services/code-review.service';
import { CodeReviewReport } from './models/report.model';

type AppMode = 'code' | 'pr';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, CodeInputComponent, ReportViewComponent, PullRequestListComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {
  mode: AppMode = 'code';

  code = '';
  fileName = 'ornek-dosya.cs';
  isLoading = false;
  errorMessage: string | null = null;
  report: CodeReviewReport | null = null;

  constructor(private readonly codeReviewService: CodeReviewService) {}

  setMode(mode: AppMode): void {
    this.mode = mode;
  }

  onAnalyze(): void {
    if (!this.code.trim()) {
      this.errorMessage = 'Lütfen önce analiz edilecek kodu girin veya bir dosya yükleyin.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    this.codeReviewService.analyze({ code: this.code, fileName: this.fileName }).subscribe({
      next: (report) => {
        this.report = report;
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage =
          err?.error?.message ??
          'API sunucusuyla iletişim kurulurken bir hata oluştu. Backend uygulamasının çalıştığından emin olun.';
        this.isLoading = false;
      },
    });
  }

  onReset(): void {
    this.report = null;
    this.errorMessage = null;
  }
}
