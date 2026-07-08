import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CodeReviewReport, ReviewRequest } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class CodeReviewService {
  // Matches the ASP.NET Core "http" launch profile (see backend/CodeInsightAI.API/Properties/launchSettings.json).
  // Adjust if the API runs on a different host/port.
  private readonly baseUrl = 'http://localhost:5228/api/codereview';

  constructor(private readonly http: HttpClient) {}

  analyze(request: ReviewRequest): Observable<CodeReviewReport> {
    return this.http.post<CodeReviewReport>(`${this.baseUrl}/analyze`, request);
  }

  downloadPdf(report: CodeReviewReport): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/report/pdf`, report, { responseType: 'blob' });
  }
}
