import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { passwordChangeGuard } from './password-change.guard';

describe('passwordChangeGuard', () => {
  function run(mustChangePassword: boolean | null, url: string): boolean | UrlTree {
    const currentUser = mustChangePassword === null ? null : { mustChangePassword };

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: () => currentUser } }]
    });

    return TestBed.runInInjectionContext(() =>
      passwordChangeGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot)
    ) as boolean | UrlTree;
  }

  it('lets a user with their own password go anywhere', () => {
    expect(run(false, '/app/projects')).toBeTrue();
  });

  it('keeps a user with an assigned password on the profile', () => {
    const result = run(true, '/app/projects');

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/app/profile');
  });

  it('lets that user open the profile itself, where the password is changed', () => {
    expect(run(true, '/app/profile')).toBeTrue();
  });

  it('lets that user open the profile also with a query in the address', () => {
    expect(run(true, '/app/profile?tab=password')).toBeTrue();
  });

  it('does not stand in the way when nobody is signed in', () => {
    expect(run(null, '/app/projects')).toBeTrue();
  });
});
