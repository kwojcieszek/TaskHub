export interface AttachmentLimits {
  maxFileSizeBytes: number;
  // 0 means there is no total limit.
  maxTotalBytesPerUser: number;
  usedBytes: number;
}

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
