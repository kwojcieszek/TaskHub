import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { TranslateModule } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { appSettings } from '../../../../core/config/app-settings';
interface Session { id: string; isCurrent: boolean; lastUsedAt: string; userAgent: string | null; ipAddress: string | null; }
@Component({ selector: 'app-sessions', standalone: true, imports: [DatePipe, Button, Card, TranslateModule], template: `
<p-card><h3 class="sessions__title"><i class="pi pi-desktop"></i>{{ 'sessions.title' | translate }}</h3>
@for (session of sessions(); track session.id) {
<div class="session"><div><strong>{{ session.isCurrent ? ('sessions.current' | translate) : ('sessions.device' | translate) }}</strong>
<p>{{ session.userAgent || ('sessions.unknown' | translate) }}</p><small>{{ session.ipAddress }} · {{ session.lastUsedAt | date:'yyyy-MM-dd HH:mm' }}</small></div>
<p-button [label]="'sessions.revoke' | translate" size="small" severity="secondary" [outlined]="true" (onClick)="revoke(session)" /></div>
}
<p-button [label]="'sessions.logoutAll' | translate" size="small" severity="danger" [outlined]="true" (onClick)="logoutAll()" />
</p-card>`, styles: [`.sessions__title{margin:0 0 .35rem;font-size:1.05rem;font-weight:500}.sessions__title i{margin-right:.5rem;color:var(--p-primary-color)}.session strong{font-weight:500}.session p{margin:.2rem 0;font-size:.85rem;color:var(--p-text-muted-color)}.session small{color:var(--p-text-muted-color)}.session {display:flex;align-items:center;justify-content:space-between;gap:1rem;padding:.75rem 0;border-bottom:1px solid var(--p-content-border-color);margin-bottom:1rem}.session p {overflow-wrap:anywhere;max-width:650px} @media(max-width:600px){.session{flex-direction:column;align-items:start}}`] })
export class SessionsComponent implements OnInit {
 private readonly http = inject(HttpClient); private readonly auth = inject(AuthService); private readonly router = inject(Router);
 readonly sessions = signal<Session[]>([]); private readonly url = `${appSettings.apiUrl}/auth/sessions`;
 ngOnInit(): void { this.load(); }
 load(): void { this.http.get<Session[]>(this.url).subscribe({next: rows => this.sessions.set(rows), error: () => {}}); }
 revoke(session: Session): void { this.http.delete(`${this.url}/${session.id}`).subscribe({next: () => { if(session.isCurrent) { this.auth.logout(); void this.router.navigate(['/login']); } else this.load(); }, error: () => {}}); }
 logoutAll(): void { this.auth.endSession(true).subscribe({next: () => void this.router.navigate(['/login']), error: () => {}}); }
}
