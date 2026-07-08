import { Component } from '@angular/core';
import { PullRequestListComponent } from './components/pull-request-list/pull-request-list.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [PullRequestListComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {}
