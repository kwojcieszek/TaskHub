export interface Attachment {
  id: string;
  projectId: string;
  taskItemId: string;
  fileName: string;
  contentType: string;
  size: number;
  sha256: string;
  uploadedById: string;
  uploadedByDisplayName: string;
  createdAt: string;
}
