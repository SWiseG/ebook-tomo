import { inject, Injectable, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

const KEY = 'tomo.lang';

export interface AppLanguage {
  code: string;
  /** Nome no próprio idioma, para o seletor. */
  label: string;
  flag: string;
}

/** Idiomas suportados pelo painel. A ordem é a exibida no seletor. */
export const LANGUAGES: readonly AppLanguage[] = [
  { code: 'pt-BR', label: 'Português', flag: '🇧🇷' },
  { code: 'en', label: 'English', flag: '🇺🇸' },
  { code: 'es', label: 'Español', flag: '🇪🇸' },
];

const DEFAULT = 'pt-BR';

/** Idioma ativo do painel: persiste a escolha e troca as traduções do Transloco ao vivo. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  readonly languages = LANGUAGES;
  readonly current = signal<string>(this.initial());

  constructor() {
    this.apply(this.current());
    // Pré-carrega todos os idiomas: troca instantânea e `translate()` síncrono confiável em TS
    // (arrays de opções e mapas de status reagem a `current()` sem corrida de carregamento).
    for (const l of LANGUAGES) this.transloco.load(l.code).subscribe();
  }

  set(code: string): void {
    if (code === this.current() || !LANGUAGES.some((l) => l.code === code)) return;
    localStorage.setItem(KEY, code);
    this.apply(code); // ativa o idioma no Transloco antes de notificar os signals dependentes
    this.current.set(code);
  }

  private initial(): string {
    const stored = localStorage.getItem(KEY);
    return stored && LANGUAGES.some((l) => l.code === stored) ? stored : DEFAULT;
  }

  private apply(code: string): void {
    this.transloco.setActiveLang(code);
    document.documentElement.lang = code;
  }
}
