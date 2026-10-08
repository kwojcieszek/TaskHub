// A token a user created on their account to sign scripts and other programs in without the password.
export interface ApiToken {
  id: string;
  name: string;
  // The first characters of the token, enough to tell tokens apart.
  prefix: string;
  createdAt: string;
  // Null for a token that never expires.
  expiresAt: string | null;
  lastUsedAt: string | null;
  expired: boolean;
}

// The answer to creating a token: the only time the token itself is sent.
export interface CreatedApiToken extends ApiToken {
  token: string;
}

export interface CreateApiTokenRequest {
  name: string;
  // Null for a token that never expires.
  expiresInDays: number | null;
}
