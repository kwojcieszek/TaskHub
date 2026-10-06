namespace TaskHub.Domain.Enums;

public enum LoginFailureReason
{
    UserNotFound = 0,
    LockedOut = 1,
    InvalidPassword = 2,
    InvalidTwoFactorCode = 3,
    GoogleAccountNotLinked = 4
}
