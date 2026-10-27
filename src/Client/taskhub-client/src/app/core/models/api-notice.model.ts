export interface ApiNotice {
  id: number;
  kind: 'error' | 'success';
  key: string;
  details?: string;
}
