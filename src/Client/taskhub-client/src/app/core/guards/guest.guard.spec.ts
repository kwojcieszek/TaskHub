import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { guestGuard } from './guest.guard';

describe('guestGuard', () => {
  function run(authenticated: boolean): boolean | UrlTree {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isAuthenticated: () => authenticated } }]
    });

    return TestBed.runInInjectionContext(() => guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as boolean | UrlTree;
  }

  it('shows the sign-in pages to someone who is signed out', () => {
    expect(run(false)).toBeTrue();
  });

  it('sends a signed-in user to the dashboard', () => {
    const result = run(true);

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/app/dashboard');
  });
});
