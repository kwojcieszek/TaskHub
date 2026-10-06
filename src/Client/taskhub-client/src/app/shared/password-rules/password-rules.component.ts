import { Component, Input, OnInit, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { PasswordRule } from '../../core/models/password-rule.model';
import { AuthService } from '../../core/services/auth.service';
import { passwordRules } from '../../core/utils/password-policy';

/** Lists the password rules set by the administrator and ticks them off as the password is typed. */
@Component({
  selector: 'app-password-rules',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './password-rules.component.html',
  styleUrl: './password-rules.component.scss'
})
export class PasswordRulesComponent implements OnInit {
  private readonly authService = inject(AuthService);

  @Input() password = '';

  ngOnInit(): void {
    // Asked again each time a password form opens, so a policy changed in the meantime is picked up.
    this.authService.loadProviders();
  }

  get rules(): PasswordRule[] {
    return passwordRules(this.password ?? '', this.authService.passwordPolicy());
  }
}
