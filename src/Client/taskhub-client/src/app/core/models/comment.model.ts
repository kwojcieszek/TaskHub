// A comment on a project or on one of its tasks.
export interface Comment {
  id: string;
  projectId: string;
  // Null for a comment on the project itself.
  taskItemId: string | null;
  // Null once the author's account has been deleted.
  authorId: string | null;
  authorDisplayName: string | null;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  // What the signed-in user may do with the comment, as decided by the server.
  canEdit: boolean;
  canDelete: boolean;
}
