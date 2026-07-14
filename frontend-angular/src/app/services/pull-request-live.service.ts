import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PullRequestChangedEvent {
  owner: string | null;
  repo: string | null;
  prNumber: number | null;
  action: string | null;
}

// Connects to the backend's PullRequestHub (see backend/CodeInsightAI.API/Hubs/PullRequestHub.cs).
// The GitHub webhook pushes a "pullRequestChanged" message here whenever a PR is opened/updated,
// so the UI can refresh the list on its own instead of requiring a manual "Yenile" click.
@Injectable({ providedIn: 'root' })
export class PullRequestLiveService implements OnDestroy {
  private readonly hubUrl = `${environment.apiBaseUrl}/hubs/pull-requests`;
  private connection: signalR.HubConnection | null = null;

  readonly pullRequestChanged = new Subject<PullRequestChangedEvent>();
  readonly connected = new Subject<boolean>();

  connect(): void {
    if (this.connection) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('pullRequestChanged', (event: PullRequestChangedEvent) => this.pullRequestChanged.next(event));
    this.connection.onreconnected(() => this.connected.next(true));
    this.connection.onreconnecting(() => this.connected.next(false));
    this.connection.onclose(() => this.connected.next(false));

    this.connection
      .start()
      .then(() => this.connected.next(true))
      .catch(() => this.connected.next(false));
  }

  ngOnDestroy(): void {
    this.connection?.stop();
  }
}
