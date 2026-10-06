import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

const PROFILE_URL = '/app/profile';

// A user signed in with an assigned password can only reach the profile, where it is replaced; the API enforces the same.
export const passwordChangeGuard: CanActivateChildFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.currentUser()?.mustChangePassword || state.url.startsWith(PROFILE_URL)) {
    return true;
  }

  return router.createUrlTree([PROFILE_URL]);
};
