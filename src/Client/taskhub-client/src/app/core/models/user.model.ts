export interface User {
  id: string;
  userName: string;
  email: string;
  displayName: string;
  twoFactorEnabled: boolean;
  mustChangePassword: boolean;
  googleLinked: boolean;
  googleEmail: string | null;
  roles: string[];
}

export interface UserSummary {
  id: string;
  userName: string;
  displayName: string;
}
