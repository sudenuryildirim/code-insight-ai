import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CodeReviewReport, PullRequestReport, PullRequestSummary, RepositoryRef } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class PullRequestService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/pullrequest`;

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

  postReviewComment(owner: string, repo: string, number: number, reviewId: string): Observable<{ posted: boolean }> {
    return this.http.post<{ posted: boolean }>(`${this.baseUrl}/${owner}/${repo}/${number}/comment/${reviewId}`, {});
  }

  downloadPdf(report: CodeReviewReport): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/report/pdf`, report, { responseType: 'blob' });
  }
}
