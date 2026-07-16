import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PromptSettings {
  prompt: string;
  isDefault: boolean;
  defaultPrompt: string;
}

// Talks to SettingsController (backend/CodeInsightAI.API/Controllers/SettingsController.cs) so the
// AI's base system prompt can be edited from within the app instead of only by changing source code.
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/settings`;

  constructor(private readonly http: HttpClient) {}

  getPrompt(): Observable<PromptSettings> {
    return this.http.get<PromptSettings>(`${this.baseUrl}/prompt`);
  }

  savePrompt(prompt: string): Observable<{ saved: boolean }> {
    return this.http.put<{ saved: boolean }>(`${this.baseUrl}/prompt`, { prompt });
  }

  resetPrompt(): Observable<{ prompt: string; isDefault: boolean }> {
    return this.http.post<{ prompt: string; isDefault: boolean }>(`${this.baseUrl}/prompt/reset`, {});
  }
}
