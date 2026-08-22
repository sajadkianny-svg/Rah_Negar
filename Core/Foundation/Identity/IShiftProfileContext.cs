namespace Rah_Negar.Foundation.Identity;

public interface IShiftProfileContext
{
    Guid ShiftProfileId { get; }
    string PersonnelNo { get; }
    string SupervisorDisplayName { get; }
    StationContext Station { get; }
    long CredentialVersion { get; }
}
