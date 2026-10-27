import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';
import { LanguageService } from './core/services/language.service';
import { ThemeService } from './core/services/theme.service';
import { UserPreferencesService } from './core/services/user-preferences.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: ThemeService, useValue: {} },
        { provide: LanguageService, useValue: {} },
        { provide: UserPreferencesService, useValue: {} }
      ]
    }).compileComponents();
  });

  it('creates the application shell with a router outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelector('router-outlet')).not.toBeNull();
  });
});
