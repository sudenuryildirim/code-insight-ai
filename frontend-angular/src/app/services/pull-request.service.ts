import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CodeReviewReport, PullRequestReport, PullRequestSummary } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class PullRequestService {
  // Matches the ASP.NET Core "http" launch profile (see backend/CodeInsightAI.API/Properties/launchSettings.json).
  private readonly baseUrl = 'http://localhost:5228/api/pullrequest';

  constructor(private readonly http: HttpClient) {}

  getOpenPullRequests(): Observable<PullRequestSummary[]> {
    return this.http.get<PullRequestSummary[]>(`${this.baseUrl}/open`);
  }

  reviewPullRequest(number: number): Observable<PullRequestReport> {
    return this.http.post<PullRequestReport>(`${this.baseUrl}/${number}/review`, {});
  }

  downloadPdf(report: CodeReviewReport): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/report/pdf`, report, { responseType: 'blob' });
  }
}
