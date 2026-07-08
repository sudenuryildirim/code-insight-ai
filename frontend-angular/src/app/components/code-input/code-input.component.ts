import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';

@Component({
  selector: 'app-code-input',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './code-input.component.html',
  styleUrl: './code-input.component.css',
})
export class CodeInputComponent {
  @Input() code = '';
  @Input() fileName = '';
  @Input() isLoading = false;
  @Input() errorMessage: string | null = null;

  @Output() codeChange = new EventEmitter<string>();
  @Output() fileNameChange = new EventEmitter<string>();
  @Output() analyze = new EventEmitter<void>();

  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  readonly features = [
    'Kodun ne işe yaradığının detaylı açıklaması',
    'Hata, mantık ve çalışma zamanı sorunu taraması',
    'Güvenlik açığı taraması (OWASP Top 10)',
    'SOLID ve Clean Code değerlendirmesi',
    'Somut çözüm önerileri ve refactor kod örnekleri',
    'İndirilebilir, ayrıntılı PDF rapor',
  ];

  onCodeInput(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.codeChange.emit(value);
  }

  onFileNameInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.fileNameChange.emit(value);
  }

  triggerFilePicker(): void {
    this.fileInput?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.fileNameChange.emit(file.name);

    const reader = new FileReader();
    reader.onload = (loadEvent) => {
      const result = loadEvent.target?.result;
      if (typeof result === 'string') {
        this.codeChange.emit(result);
      }
    };
    reader.readAsText(file);

    // allow re-selecting the same file later
    input.value = '';
  }

  onAnalyzeClick(): void {
    this.analyze.emit();
  }
}
