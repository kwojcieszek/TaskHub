import { Component, OnInit, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Button } from 'primeng/button';
import { finalize } from 'rxjs';
import { appSettings } from '../../../core/config/app-settings';
import { ProjectMember } from '../../../core/models/project.model';
interface Change { id: string; changedAt: string; actorName: string; field: string; oldValue: string | null; newValue: string | null; }
@Component({selector:'app-task-history', standalone:true, imports:[DatePipe, TranslateModule, Button], template:`
<section><h3 class="history__title"><i class="pi pi-history"></i>{{ 'history.title' | translate }}</h3>
@for (change of changes(); track change.id) {
<div class="change"><small>{{ change.changedAt | date:'yyyy-MM-dd HH:mm' }} · {{ change.actorName || ('history.system' | translate) }}</small>
<p><strong>{{ 'history.fields.' + change.field | translate }}</strong></p>
<p class="values">{{ value(change, change.oldValue) }} → {{ value(change, change.newValue) }}</p></div>
}
@if (!busy() && changes().length === 0 && !failed()) { <p>{{ 'history.empty' | translate }}</p> }
@if (more() || failed()) { <p-button [label]="(failed() ? 'history.retry' : 'history.more') | translate" [loading]="busy()" [disabled]="busy()" severity="secondary" (onClick)="load()" /> }
</section>`,styles:[`.history__title{margin:0 0 .25rem;font-size:1rem;font-weight:500}.history__title i{margin-right:.5rem;color:var(--p-primary-color)}.change:last-child{border-bottom:0}.change{border-bottom:1px solid var(--p-content-border-color);padding:.6rem 0}.change p{margin:.3rem 0}.values{white-space:pre-wrap;overflow-wrap:anywhere;max-height:200px;overflow:auto}small{opacity:.75}`]})
export class TaskHistoryComponent implements OnInit {
 readonly projectId = input.required<string>(); readonly taskId = input.required<string>(); readonly members = input<ProjectMember[]>([]);
 readonly changes = signal<Change[]>([]); readonly more = signal(true); readonly busy = signal(false); readonly failed = signal(false);
 private readonly http = inject(HttpClient); private readonly translate = inject(TranslateService); private page = 1;
 ngOnInit(): void { this.load(); }
 load(): void { if(this.busy()) return; this.busy.set(true); this.failed.set(false);
 this.http.get<Change[]>(`${appSettings.apiUrl}/projects/${this.projectId()}/tasks/${this.taskId()}/history?page=${this.page}`).pipe(finalize(() => this.busy.set(false))).subscribe({next: rows => {this.changes.update(old => [...old, ...rows]); this.more.set(rows.length === 50); this.page++;}, error: () => this.failed.set(true)}); }
 value(change: Change, value: string | null): string {
 if(!value) return '—';
 if(change.field === 'Status') return this.translate.instant('tasks.status.' + value);
 if(change.field === 'Priority') return this.translate.instant('tasks.priority.' + value);
 if(change.field === 'AssignedUserId') return this.members().find(m => m.userId === value)?.displayName ?? this.translate.instant('history.formerMember');
 if(change.field === 'ParentTaskId') return this.translate.instant('history.parent');
 if(change.field === 'DueDate') return new Date(value).toLocaleDateString();
 return value;
 }
}
