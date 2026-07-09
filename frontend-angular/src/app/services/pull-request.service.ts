import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CodeReviewReport, PullRequestReport, PullRequestSummary, RepositoryRef } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class PullRequestService {
  // Matches the ASP.NET Core "http" launch profile (see backend/CodeInsightAI.API/Properties/launchSettings.json).
  private readonly baseUrl = 'http://localhost:5228/api/pullrequest';

  constructor(private readonly http: HttpClient) {}

  getRepositories(): Observable<RepositoryRef[]> {
    return this.http.get<RepositoryRef[]>(`${this.baseUrl}/repos`);
  }

  getOpenPullRequests(owner: string, repo: string): Observable<PullRequestSummary[]> {
    return this.http.get<PullRequestSummary[]>(`${this.baseUrl}/${owner}/${repo}/open`);
  }

  reviewPullRequest(owner: string, repo: string, number: number, force = false): Observable<PullRequestReport> {
    const query = force ? '?force=true' : '';
    return this.http.post<PullRequestReport>(`${this.baseUrl}/${owner}/${repo}/${number}/review${query}`, {});
  }

  getReviewHistory(owner: string, repo: string, number: number): Observable<PullRequestReport[]> {
    return this.http.get<PullRequestReport[]>(`${this.baseUrl}/${owner}/${repo}/${number}/history`);
  }

  downloadPdf(report: CodeReviewReport): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/report/pdf`, report, { responseType: 'blob' });
  }
}
