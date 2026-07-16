import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { SettingsService } from '../../services/settings.service';

@Component({
  selector: 'app-prompt-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './prompt-settings.component.html',
  styleUrl: './prompt-settings.component.css',
})
export class PromptSettingsComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();

  promptText = '';
  isDefault = true;
  isLoading = true;
  isSaving = false;
  isResetting = false;
  error: string | null = null;
  savedNotice = false;

  constructor(private readonly settingsService: SettingsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.error = null;

    this.settingsService.getPrompt().subscribe({
      next: (settings) => {
        this.promptText = settings.prompt;
        this.isDefault = settings.isDefault;
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Prompt alınamadı. Backend servisini kontrol edin.';
        this.isLoading = false;
      },
    });
  }

  save(): void {
    if (!this.promptText.trim()) {
      this.error = 'Prompt boş olamaz.';
      return;
    }

    this.isSaving = true;
    this.error = null;
    this.savedNotice = false;

    this.settingsService.savePrompt(this.promptText).subscribe({
      next: () => {
        this.isSaving = false;
        this.isDefault = false;
        this.savedNotice = true;
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Prompt kaydedilirken bir hata oluştu.';
        this.isSaving = false;
      },
    });
  }

  resetToDefault(): void {
    this.isResetting = true;
    this.error = null;
    this.savedNotice = false;

    this.settingsService.resetPrompt().subscribe({
      next: (result) => {
        this.promptText = result.prompt;
        this.isDefault = result.isDefault;
        this.isResetting = false;
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Varsayılana döndürülürken bir hata oluştu.';
        this.isResetting = false;
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
