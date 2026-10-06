import { Component, Input } from '@angular/core';
import { isImageIcon } from '../../core/utils/image-icon';

@Component({
  selector: 'app-project-avatar',
  standalone: true,
  templateUrl: './project-avatar.component.html',
  styleUrl: './project-avatar.component.scss'
})
export class ProjectAvatarComponent {
  @Input({ required: true }) name!: string;
  @Input() icon: string | null = null;
  @Input() color: string | null = null;
  @Input() large = false;

  get image(): string | null {
    return isImageIcon(this.icon) ? this.icon : null;
  }
}
