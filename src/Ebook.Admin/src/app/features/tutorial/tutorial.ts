import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';
import { AccordionModule } from 'primeng/accordion';

interface Step {
  icon: string;
  title: string;
  text: string;
  tag: string;
}
interface Area {
  icon: string;
  title: string;
  text: string;
  link: string;
  linkLabel: string;
}
interface Faq {
  q: string;
  a: string;
}

@Component({
  selector: 'app-tutorial',
  imports: [
    RouterLink,
    TranslocoDirective,
    CardModule,
    ButtonModule,
    TagModule,
    TimelineModule,
    AccordionModule,
  ],
  templateUrl: './tutorial.html',
  styleUrl: './tutorial.scss',
})
export class Tutorial {
  readonly steps: Step[] = [
    {
      icon: 'pi pi-compass',
      title: 'Descobrir nichos',
      text: 'O motor de tendências coleta sinais (Reddit, autocomplete e mais), pontua e ranqueia nichos. Você pode disparar a descoberta manualmente.',
      tag: 'Automático · mensal',
    },
    {
      icon: 'pi pi-check-circle',
      title: 'Aprovar um nicho',
      text: 'Revise os candidatos em Nichos e aprove o que fizer sentido. Aprovar libera o nicho para virar produto.',
      tag: 'Você decide',
    },
    {
      icon: 'pi pi-sparkles',
      title: 'Gerar o produto',
      text: 'Em um nicho, clique em “Gerar produto”. A IA cria o KnowledgePack, o outline e escreve os capítulos um a um.',
      tag: 'IA',
    },
    {
      icon: 'pi pi-book',
      title: 'Pipeline de conteúdo',
      text: 'Revisão editorial, capa + mockup, e o PDF comercial com tema por nicho. Acompanhe cada etapa na página do produto.',
      tag: 'Outline → PDF',
    },
    {
      icon: 'pi pi-desktop',
      title: 'Landing page',
      text: 'Uma LP de alta conversão é gerada com a copy e a capa do produto, pronta para publicar. Abra-a direto pelo painel.',
      tag: 'E06',
    },
    {
      icon: 'pi pi-send',
      title: 'Aprovar publicação',
      text: 'No modo padrão, o produto aguarda sua aprovação antes de ir ao ar. No modo Auto, segue sozinho para publicação.',
      tag: 'Gate de aprovação',
    },
  ];

  readonly areas: Area[] = [
    {
      icon: 'pi pi-th-large',
      title: 'Dashboard',
      text: 'Visão geral: produtos ativos, pipeline, jobs falhos e consumo de IA.',
      link: '/dashboard',
      linkLabel: 'Abrir dashboard',
    },
    {
      icon: 'pi pi-compass',
      title: 'Nichos',
      text: 'Descobrir, aprovar, descartar e gerar produtos a partir de nichos.',
      link: '/niches',
      linkLabel: 'Ver nichos',
    },
    {
      icon: 'pi pi-book',
      title: 'Produtos',
      text: 'Acompanhe o pipeline, leia o manuscrito, baixe o PDF e abra a LP.',
      link: '/products',
      linkLabel: 'Ver produtos',
    },
    {
      icon: 'pi pi-bolt',
      title: 'Jobs',
      text: 'Fila de trabalhos com retry e dead-letter. Reprocesse jobs falhos.',
      link: '/jobs',
      linkLabel: 'Ver jobs',
    },
    {
      icon: 'pi pi-sliders-h',
      title: 'Configurações',
      text: 'Tetos de IA, parâmetros de descoberta e gate de publicação (JSON).',
      link: '/settings',
      linkLabel: 'Ajustar',
    },
  ];

  readonly faqs: Faq[] = [
    {
      q: 'Preciso aprovar cada produto antes de publicar?',
      a: 'Por padrão, sim — o produto fica em “Aguardando aprovação” ao fim do pipeline. Você pode ligar o modo Auto em Configurações (publishing.requiresApproval = false) para publicar sem gate.',
    },
    {
      q: 'Quanto custa de IA gerar um produto?',
      a: 'O AI Gateway consulta cache e base de conhecimento antes de chamar o modelo, e há um teto de chamadas por pipeline configurável. O consumo do dia aparece no Dashboard.',
    },
    {
      q: 'Um job falhou. E agora?',
      a: 'Jobs com retry esgotado vão para dead-letter. Abra Jobs, filtre por “Dead” e clique em “Reprocessar”. O pipeline é idempotente: reprocessar não duplica artefatos.',
    },
    {
      q: 'Posso trocar o tema e recolher o menu?',
      a: 'Sim. Use o botão de tema (sol/lua) na barra lateral ou no topo, e o botão de recolher para alternar entre o menu completo e o modo compacto (só ícones).',
    },
  ];
}
