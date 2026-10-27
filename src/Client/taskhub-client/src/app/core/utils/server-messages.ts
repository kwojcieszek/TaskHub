// The API reports errors as English sentences. The ones a user can run into from the interface are shown
// in the interface language; anything not listed here is shown as the server sent it.
const SERVER_MESSAGE_KEYS: Record<string, string> = {
  'User is already a member of this project.': 'serverErrors.alreadyProjectMember',
  'User is already a member of this group.': 'serverErrors.alreadyTeamMember',
  'The team already has access to this project.': 'serverErrors.teamAlreadyAdded',
  'The project owner cannot be removed.': 'serverErrors.ownerCannotBeRemoved',
  'A team can be added as Editor or Viewer.': 'serverErrors.teamRole',
  'A task can only be assigned to a member of the project.': 'serverErrors.assigneeNotMember',
  'The task is already completed.': 'serverErrors.taskAlreadyCompleted',
  'Only a task that has not been started yet can be started.': 'serverErrors.taskAlreadyStarted',
  'A task with subtasks is started and completed through its subtasks.': 'serverErrors.taskFollowsSubTasks',
  'The comment cannot be empty.': 'serverErrors.commentEmpty',
  'File is empty.': 'serverErrors.fileEmpty',
  'Invalid authenticator code.': 'serverErrors.invalidAuthenticatorCode',
  'Invalid password.': 'serverErrors.invalidPassword',
  'The new password must differ from the current one.': 'serverErrors.passwordUnchanged',
  'Two-factor authentication is already enabled. Disable it first to set it up again.': 'serverErrors.twoFactorAlreadyEnabled',
  'Confirm your password and authenticator code before linking Google.': 'serverErrors.confirmBeforeGoogle',
  'This Google account is already linked to another user.': 'serverErrors.googleLinkedElsewhere',
  'A Google account is already linked to this user.': 'serverErrors.googleAlreadyLinked',
  'The Google account could not be verified.': 'serverErrors.googleNotVerified',
  'The Google account has no verified e-mail address.': 'serverErrors.googleEmailNotVerified',
  'Transfer team ownership before leaving.': 'serverErrors.transferBeforeLeaving',
  'The new owner must be a member of the team.': 'serverErrors.newOwnerNotMember',
  'You cannot remove the administrator role from your own account.': 'serverErrors.ownAdminRole',
  'You cannot lock your own account.': 'serverErrors.ownLock',
  'You cannot delete your own account.': 'serverErrors.ownDelete',
  "Transfer or remove the user's projects and teams before deleting this account.": 'serverErrors.userOwnsProjects',
  "Remove the user's attachments before deleting this account.": 'serverErrors.userHasAttachments'
};

export function serverMessageKey(message: string): string | null {
  return Object.hasOwn(SERVER_MESSAGE_KEYS, message) ? SERVER_MESSAGE_KEYS[message] : null;
}
