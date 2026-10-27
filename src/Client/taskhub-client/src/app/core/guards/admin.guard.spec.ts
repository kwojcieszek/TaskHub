import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { adminGuard } from './admin.guard';

describe('adminGuard', () => {
  function run(admin: boolean): boolean | UrlTree {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isAdmin: () => admin } }]
    });

    return TestBed.runInInjectionContext(() => adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as boolean | UrlTree;
  }

  it('lets an administrator into the admin panel', () => {
    expect(run(true)).toBeTrue();
  });

  it('sends everyone else to the projects', () => {
    const result = run(false);

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/app/projects');
  });
});
