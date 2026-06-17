import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/language.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {
  // Instancia o idioma o quanto antes (define o lang ativo do Transloco antes do primeiro render).
  private readonly language = inject(LanguageService);
}
