import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { PullRequestService } from '../../services/pull-request.service';
import { CodeReviewReport, IssueCategory, IssueSeverity, ReviewIssue } from '../../models/report.model';

const SEVERITY_RANK: Record<IssueSeverity, number> = {
  Critical: 0,
  Error: 1,
  Warning: 2,
  Info: 3,
};

const CATEGORY_LABELS: Record<IssueCategory, string> = {
  Bug: 'Hata (Bug)',
  Security: 'Güvenlik',
  Performance: 'Performans',
  SOLID: 'SOLID',
  CleanCode: 'Temiz Kod',
  Refactoring: 'Refactoring',
  CodeSmell: 'Code Smell',
  Testing: 'Test',
  General: 'Genel',
};

@Component({
  selector: 'app-report-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './report-view.component.html',
  styleUrl: './report-view.component.css',
})
export class ReportViewComponent {
  @Input({ required: true }) report!: CodeReviewReport;
  @Input() resetLabel = 'Listeye Dön';

  @Output() reset = new EventEmitter<void>();

  selectedCategory: IssueCategory | null = null;
  selectedIssue: ReviewIssue | null = null;
  pdfLoading = false;
  pdfError: string | null = null;

  readonly categoryOrder: IssueCategory[] = [
    'Bug',
    'Security',
    'Performance',
    'SOLID',
    'CleanCode',
    'CodeSmell',
    'Refactoring',
    'Testing',
    'General',
  ];

  constructor(private readonly pullRequestService: PullRequestService) {}

  categoryLabel(category: IssueCategory): string {
    return CATEGORY_LABELS[category] ?? category;
  }

  get sortedIssues(): ReviewIssue[] {
    return [...this.report.issues].sort(
      (a, b) => SEVERITY_RANK[a.severity] - SEVERITY_RANK[b.severity] || a.lineNumber - b.lineNumber,
    );
  }

  get filteredIssues(): ReviewIssue[] {
    return this.selectedCategory
      ? this.sortedIssues.filter((issue) => issue.category === this.selectedCategory)
      : this.sortedIssues;
  }

  get categoryCounts(): Partial<Record<IssueCategory, number>> {
    const counts: Partial<Record<IssueCategory, number>> = {};
    for (const issue of this.report.issues) {
      counts[issue.category] = (counts[issue.category] ?? 0) + 1;
    }
    return counts;
  }

  get scoreClass(): string {
    if (this.report.score >= 80) return 'score-good';
    if (this.report.score >= 50) return 'score-medium';
    return 'score-bad';
  }

  selectCategory(category: IssueCategory | null): void {
    this.selectedCategory = category;
  }

  selectIssue(issue: ReviewIssue): void {
    this.selectedIssue = issue;
  }

  severityBadgeClass(severity: IssueSeverity): string {
    switch (severity) {
      case 'Critical':
        return 'badge-critical';
      case 'Error':
        return 'badge-error';
      case 'Warning':
        return 'badge-warning';
      default:
        return 'badge-info';
    }
  }

  onReset(): void {
    this.reset.emit();
  }

  downloadPdf(): void {
    this.pdfLoading = true;
    this.pdfError = null;

    this.pullRequestService.downloadPdf(this.report).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        const safeName = (this.report.fileName || 'rapor').replace(/\s+/g, '-');
        anchor.href = url;
        anchor.download = `pr-inceleme-raporu-${safeName}.pdf`;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
        this.pdfLoading = false;
      },
      error: () => {
        this.pdfError = 'PDF oluşturulurken bir hata oluştu. Backend servisinin çalıştığından emin olun.';
        this.pdfLoading = false;
      },
    });
  }
}
