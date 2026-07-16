import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { PullRequestService } from '../../services/pull-request.service';
import { PullRequestLiveService } from '../../services/pull-request-live.service';
import { ReportViewComponent } from '../report-view/report-view.component';
import { CodeReviewReport, PullRequestReport, PullRequestSummary, RepositoryRef } from '../../models/report.model';

@Component({
  selector: 'app-pull-request-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReportViewComponent],
  templateUrl: './pull-request-list.component.html',
  styleUrl: './pull-request-list.component.css',
})
export class PullRequestListComponent implements OnInit, OnDestroy {
  repositories: RepositoryRef[] = [];
  selectedRepo: RepositoryRef | null = null;
  reposError: string | null = null;

  pullRequests: PullRequestSummary[] = [];
  isLoadingList = false;
  listError: string | null = null;

  reviewingNumber: number | null = null;
  reviewError: string | null = null;
  selectedReport: PullRequestReport | null = null;
  reviewHistory: PullRequestReport[] = [];
  isReanalyzing = false;

  isPostingComment = false;
  commentPosted = false;
  commentError: string | null = null;

  isLive = false;
  private readonly liveSubscriptions = new Subscription();

  // Optional free-text instruction that steers the AI's focus for the next analysis (e.g. "sadece
  // güvenlik açıklarına odaklan") - sent alongside the diff instead of relying purely on the fixed
  // built-in checklist. Applies to whichever PR's "İncele"/"Yeniden Analiz Et" is clicked next.
  customInstruction = '';

  // The PR the user has picked from the list to run the query composer against - selecting is a
  // separate step from analyzing, so they can type/edit an instruction before triggering it.
  selectedPrForQuery: PullRequestSummary | null = null;

  constructor(
    private readonly pullRequestService: PullRequestService,
    private readonly liveService: PullRequestLiveService,
  ) {}

  ngOnInit(): void {
    this.loadRepositories();

    this.liveService.connect();
    this.liveSubscriptions.add(this.liveService.connected.subscribe((connected) => (this.isLive = connected)));
    this.liveSubscriptions.add(
      this.liveService.pullRequestChanged.subscribe((event) => {
        const matchesSelectedRepo =
          !this.selectedRepo || (event.owner === this.selectedRepo.owner && event.repo === this.selectedRepo.repo);

        // Only auto-refresh the list view - don't yank the user out of a report they're reading,
        // and ignore changes for repos other than the one currently selected.
        if (!this.selectedReport && matchesSelectedRepo) {
          this.loadPullRequests();
        }
      }),
    );
  }

  ngOnDestroy(): void {
    this.liveSubscriptions.unsubscribe();
  }

  loadRepositories(): void {
    this.reposError = null;

    this.pullRequestService.getRepositories().subscribe({
      next: (repositories) => {
        this.repositories = repositories;
        if (repositories.length > 0) {
          this.selectRepo(repositories[0]);
        } else {
          this.reposError = 'Yapılandırılmış organizasyonda repo bulunamadı. appsettings.Development.json içindeki GitHub:Organization değerini kontrol edin.';
        }
      },
      error: (err) => {
        this.reposError = err?.error?.message ?? 'Repo listesi alınamadı. Backend servisini kontrol edin.';
      },
    });
  }

  selectRepo(repo: RepositoryRef): void {
    this.selectedRepo = repo;
    this.backToList();
    this.loadPullRequests();
  }

  loadPullRequests(): void {
    if (!this.selectedRepo) {
      return;
    }
    const { owner, repo } = this.selectedRepo;

    this.isLoadingList = true;
    this.listError = null;
    this.selectedPrForQuery = null;

    this.pullRequestService.getOpenPullRequests(owner, repo).subscribe({
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

  selectPrForQuery(pr: PullRequestSummary): void {
    this.selectedPrForQuery = pr;
  }

  runQuery(): void {
    if (!this.selectedPrForQuery) {
      return;
    }
    this.reviewPullRequest(this.selectedPrForQuery.number);
  }

  reviewPullRequest(number: number, force = false): void {
    if (!this.selectedRepo) {
      return;
    }
    const { owner, repo } = this.selectedRepo;

    if (force) {
      this.isReanalyzing = true;
    } else {
      this.reviewingNumber = number;
    }
    this.reviewError = null;

    this.pullRequestService.reviewPullRequest(owner, repo, number, force, this.customInstruction).subscribe({
      next: (report) => {
        this.selectedReport = report;
        this.reviewingNumber = null;
        this.isReanalyzing = false;
        this.commentPosted = false;
        this.commentError = null;
        this.loadHistory(number);
      },
      error: (err) => {
        this.reviewError = err?.error?.message ?? `PR #${number} incelenirken bir hata oluştu.`;
        this.reviewingNumber = null;
        this.isReanalyzing = false;
      },
    });
  }

  reanalyze(): void {
    if (this.selectedReport) {
      this.reviewPullRequest(this.selectedReport.prNumber, true);
    }
  }

  loadHistory(number: number): void {
    if (!this.selectedRepo) {
      return;
    }
    const { owner, repo } = this.selectedRepo;

    this.pullRequestService.getReviewHistory(owner, repo, number).subscribe({
      next: (history) => (this.reviewHistory = history),
      error: () => (this.reviewHistory = []),
    });
  }

  selectHistoryEntry(entry: PullRequestReport): void {
    this.selectedReport = entry;
    this.commentPosted = false;
    this.commentError = null;
  }

  postComment(): void {
    if (!this.selectedRepo || !this.selectedReport) {
      return;
    }
    const { owner, repo } = this.selectedRepo;

    this.isPostingComment = true;
    this.commentError = null;

    this.pullRequestService.postReviewComment(owner, repo, this.selectedReport.prNumber, this.selectedReport.id).subscribe({
      next: () => {
        this.isPostingComment = false;
        this.commentPosted = true;
      },
      error: (err) => {
        this.commentError = err?.error?.message ?? 'Yorum GitHub\'a gönderilirken bir hata oluştu.';
        this.isPostingComment = false;
      },
    });
  }

  backToList(): void {
    this.selectedReport = null;
    this.reviewHistory = [];
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
