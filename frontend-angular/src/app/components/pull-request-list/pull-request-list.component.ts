import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { PullRequestService } from '../../services/pull-request.service';
import { ReportViewComponent } from '../report-view/report-view.component';
import { CodeReviewReport, PullRequestReport, PullRequestSummary } from '../../models/report.model';

@Component({
  selector: 'app-pull-request-list',
  standalone: true,
  imports: [CommonModule, ReportViewComponent],
  templateUrl: './pull-request-list.component.html',
  styleUrl: './pull-request-list.component.css',
})
export class PullRequestListComponent implements OnInit {
  pullRequests: PullRequestSummary[] = [];
  isLoadingList = false;
  listError: string | null = null;

  reviewingNumber: number | null = null;
  reviewError: string | null = null;
  selectedReport: PullRequestReport | null = null;

  constructor(private readonly pullRequestService: PullRequestService) {}

  ngOnInit(): void {
    this.loadPullRequests();
  }

  loadPullRequests(): void {
    this.isLoadingList = true;
    this.listError = null;

    this.pullRequestService.getOpenPullRequests().subscribe({
      next: (pullRequests) => {
        this.pullRequests = pullRequests;
        this.isLoadingList = false;
      },
      error: (err) => {
        this.listError =
          err?.error?.message ?? 'Açık pull request listesi alınamadı. GitHub yapılandırmasını ve backend servisini kontrol edin.';
        this.isLoadingList = false;
      },
    });
  }

  reviewPullRequest(number: number): void {
    this.reviewingNumber = number;
    this.reviewError = null;

    this.pullRequestService.reviewPullRequest(number).subscribe({
      next: (report) => {
        this.selectedReport = report;
        this.reviewingNumber = null;
      },
      error: (err) => {
        this.reviewError = err?.error?.message ?? `PR #${number} incelenirken bir hata oluştu.`;
        this.reviewingNumber = null;
      },
    });
  }

  backToList(): void {
    this.selectedReport = null;
  }

  // The existing report-view component was built for single-file CodeReviewReport results;
  // this maps a PullRequestReport onto that same shape so the rendering (score, issue list,
  // filters, PDF export) can be reused as-is instead of duplicating it for PRs.
  get adaptedReport(): CodeReviewReport | null {
    const report = this.selectedReport;
    if (!report) {
      return null;
    }

    return {
      id: report.id,
      fileName: `PR #${report.prNumber} — ${report.prTitle}`,
      detectedLanguage: `${report.headBranch} → ${report.baseBranch}`,
      score: report.reliabilityScore,
      codePurpose: report.detectedPurpose,
      summary: `Değerlendirme: ${report.verdict}\n\n${report.summary}`,
      strengths: [],
      issues: report.issues,
      recommendations: report.recommendations,
      createdAt: report.createdAt,
    };
  }
}
